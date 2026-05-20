# LuoXi 脸部渲染 EID 960 着色器分析

## 1. Draw 与管线概况

分析对象来自 `D:\ProgramData\RenderXtData\LuoXi.rdc`：

| 项目 | 值 |
|---|---|
| Draw | EID 960, `vkCmdDrawIndexed()` |
| 索引数 / 实例数 | 10590 / 1 |
| RenderDoc flags | `Drawcall`, `Indexed`, `Instanced` |
| Topology | `TriangleStrip` |
| 上一个 / 下一个事件 | EID 952 / EID 965 |
| 顶点着色器 | `ResourceId::40468`, entry `main` |
| 片元着色器 | `ResourceId::40469`, entry `main` |
| VS 反编译附录 | `doc/附录/LuoXi_face_EID960_vs_spirv.txt` |
| PS 反编译附录 | `doc/附录/LuoXi_face_EID960_ps_spirv.txt` |
| 输出 RT0 | `ResourceId::33297`, 2560x1440, `R11G11B10_FLOAT` |
| 输出 RT1 | `ResourceId::33259`, 2560x1440, `R10G10B10A2_UNORM` |
| 深度/模板 | `ResourceId::33254`, 2560x1440, `D32S8` |

RenderDoc MCP 对该捕获的部分固定功能状态读取存在接口限制，blend/depth/stencil/rasterizer 细节未能完整返回；本文主要依据 shader reflection、资源绑定和 SPIR-V 反编译逻辑分析。

该 draw 是一个角色脸部材质 pass。片元阶段不是单纯写 GBuffer，而是包含大量面部风格化/皮肤光照逻辑、阴影采样、LUT/体积纹理采样和雾/大气合成，最终写两个 render target：

- `RT0`：最终或中间 HDR 颜色，`R11G11B10_FLOAT`。
- `RT1`：辅助 GBuffer/材质信息，`R10G10B10A2_UNORM`。反编译中 `_1448` 被写到 `*_15`，其构造近似为 `float4(screen_or_motion_xy, 1.0, material_flag)`，alpha 在正反面间选择 `0.7/0.4`。

## 2. 顶点着色器

### 2.1 输入布局

| 输入 | VB | Offset | Format | 推断用途 |
|---|---:|---:|---|---|
| `_input0` | 0 | 0 | `R32G32B32_FLOAT` | 位置 |
| `_input2` | 0 | 12 | `R32_FLOAT` | 压缩法线/附加打包数据的一部分 |
| `_input3` | 4 | 12 | `R8G8B8A8_UNORM` | 顶点颜色或材质 mask |
| `_input1` | 1 | 0 | `R32G32_FLOAT` | 主 UV |
| `_input4` | 1 | 8 | `R8G8B8A8_SNORM` | 切线/符号或压缩切线 |
| `_input5` | 3 | 0 | `R32G32B32_FLOAT` | 备用位置/形变位置 |
| `_input6` | 3 | 12 | `R32_FLOAT` | 压缩法线/形变数据 |
| `_input8` | 2 | 0 | `R16G16B16A16_UNORM` | 骨骼权重 |
| `_input9` | 2 | 8 | `R8G8B8A8_UINT` | 骨骼索引 |
| `_InstanceIndex` | built-in | - | `UInt` | 实例索引 |

索引和多个 vertex buffer 都来自 `ResourceId::29635`，另有 `ResourceId::135` 作为 VB4。顶点着色器还读取 `ssbo29`，用于骨骼矩阵或实例相关矩阵数据。

### 2.2 常量和 SSBO

| 资源 | Bind point | 大小 | 用途推断 |
|---|---:|---:|---|
| `uniforms24` | 14 | 4512 | frame/view 级矩阵、相机、屏幕、环境参数 |
| `uniforms31` | 0 | 384 | material/object 小参数块，含 UV transform |
| `uniforms27` | 0 | 65536 | 256 个 instance/object 结构，每个约 256 bytes |
| `ssbo29` | 16 | buffer | 骨骼或形变矩阵数组 |

### 2.3 主要逻辑

顶点阶段包含以下步骤：

1. **压缩法线/切线解码**  
   `_input2.x` 和 `_input6.x` 被 `Bitcast` 为 `uint` 后按 10-bit 分量拆解，并执行 octahedral normal decode：
   - 取 10-bit x/y/z 或 x/y 分量。
   - 乘 `0.002` 映射到约 `[-1, 1]`。
   - 使用 `abs`、`step`、`select` 折叠半球。
   - `Normalize` 得到单位向量。

   关键反编译证据：

   ```text
   float3 _151 = _150 * 0.0020;
   float _157 = 1.0000 - abs(_151.x) - abs(_151.y);
   float2 _169 = Select(_160, _168, _164);
   float3 _171 = Normalize(float3(_169.x, _169.y, _158.z));
   ```

2. **切线基重建**  
   shader 以法线为基准构造两个正交方向，通过 cross/normalize 得到切线空间辅助向量，再由打包符号恢复切线 w：

   ```text
   float3 _179 = Normalize(_178);
   float3 _181 = Normalize(Cross(_171, _179));
   float3x2 _192 = {_179, _181};
   float3 _193 = _192 * Normalize(float2(...));
   ```

3. **蒙皮/实例矩阵变换**  
   shader 根据 `_InstanceIndex` 读取 `uniforms27` 中当前实例的结构。若实例 flag 开启，会从 `ssbo29` 读取骨骼矩阵，并按 `_input8` 权重和 `_input9` 骨骼索引做 2 或 4 bone blend。随后分别变换位置、法线和切线。

4. **世界空间和裁剪空间输出**  
   变换后的位置经过 object/world/view-projection 相关矩阵得到 clip position。shader 还对屏幕偏移/viewport 参数进行修正，并最终翻转 y：

   ```text
   float4 _455 = float4(world_minus_origin, 1.0) * uniforms24._child8;
   float2 _463 = _455.xy - uniforms24._child31.zw * {2,-2} * _455.w;
   _Position = float4(_463.x, -_463.y, _455.z, _455.w);
   ```

5. **Varying 输出**  
   输出到片元阶段的 varyings 包括：
   - `_output0`：UV，已应用 `uniforms31._child28.xy/zw` 的 scale/bias。
   - `_output1`：世界空间位置。
   - `_output2`：世界空间法线。
   - `_output3`：世界空间切线和 handedness。
   - `_output4`、`_output5`：屏幕/投影相关坐标。
   - `_output6`：另一组解码法线或面部方向数据。
   - `_output7`：顶点颜色/材质 mask。
   - `_output8`：flat instance index。

## 3. 片元着色器资源

### 3.1 常量块与 buffer

| 资源 | Bind point | 大小 | 用途推断 |
|---|---:|---:|---|
| `uniforms34` | 12 | 32864 | 光照/cluster 或 light list 参数 |
| `uniforms36` | 13 | 11440 | 多光源、阴影、投影矩阵、区域光参数 |
| `uniforms17` | 14 | 4512 | frame/view、相机、环境、雾、大气、全局材质调制 |
| `uniforms32` | 38 | 48 | light list/loop 控制参数 |
| `uniforms65` | 39 | 2560 | shadow/atlas 或投影矩阵数组 |
| `uniforms47` | 0 | 384 | 脸部材质参数块 |
| `uniforms20` | 0 | 65536 | per-instance/object 数据 |
| `ssbo30` | 16 | buffer | object/骨骼或附加只读 buffer |
| `ssbo28` | 40 | buffer | light list 或 tile/cluster 索引 |

MCP 返回的 cbuffer value 多数为 0，疑似受捕获/读取接口限制影响；本文不依赖这些数值做参数定量，只按反编译访问模式分析用途。

### 3.2 贴图绑定

| Slot | 名称 | Bind point | 类型 | 尺寸/格式 | 逻辑用途推断 |
|---:|---|---:|---|---|---|
| 0 | `res38` | 18 | 2D | 2560x1440 `R8G8_UNORM` | 屏幕空间 buffer，参与遮蔽/环境或深度相关修正 |
| 1 | `res37` | 20 | 2D | 6144x4096 `D16` | shadow map，使用 `ImageSampleDrefExplicitLod` |
| 2 | `res63` | 22 | 2D | 4x4 `R8G8B8A8_SRGB` | 小 LUT/默认贴图 |
| 3 | `res45` | 23 | 3D | 128x192 `R8G8B8A8_UNORM` | 体积 LUT/环境探针 |
| 4 | `res43` | 24 | 3D | 128x192 `R8G8B8A8_UNORM` | 体积 LUT/环境探针 |
| 5 | `res41` | 25 | 3D | 128x192 `R8G8B8A8_UNORM` | 体积 LUT/环境探针 |
| 6 | `res44` | 26 | 3D | 128x64 `R11G11B10_FLOAT` | HDR 体积光照/SH |
| 7 | `res42` | 27 | 3D | 128x64 `R11G11B10_FLOAT` | HDR 体积光照/SH |
| 8 | `res40` | 28 | 3D | 128x64 `R11G11B10_FLOAT` | HDR 体积光照/SH |
| 9 | `res68` | 29 | 3D | 1x1 `R8G8B8A8_SRGB` | fallback 3D LUT |
| 10 | `res54` | 33 | 2D | 256x256 `BC7_UNORM` | 面部细节/mask |
| 11 | `res53` | 34 | 2D | 256x256 `BC7_UNORM` | 面部动画/细节 mask |
| 12 | `res51` | 5 | 2D | 1024x32 `BC7_SRGB` | 颜色 ramp / 2D LUT |
| 13 | `res61` | 6 | 2D | 256x256 `BC7_UNORM` | mask/法线/材质控制 |
| 14 | `res59` | 7 | 2D | 1024x1024 `R8G8B8A8_UNORM` | ramp/方向 mask/面部阴影贴图 |
| 15 | `res49` | 8 | 2D | 512x512 `BC7_UNORM` | 细节/噪声/法线 |
| 16 | `res50` | 9 | 2D | 1024x1024 `BC7_SRGB` | 表情/面部 overlay，按 2x2 atlas 采样 |
| 17 | `res48` | 10 | 2D | 256x1 `R8G8B8A8_UNORM` | 1D ramp，用于 toon/face shade |
| 18 | `res57` | 11 | 2D | 1024x1024 `BC7_SRGB` | 主 albedo/base color |
| 19 | `res56` | 0 | 2D | 512x512 `BC3_SRGB` | 三平面或环境颜色贴图 |
| 20 | `res55` | 1 | 2D | 1024x1024 `BC7_UNORM` | 三平面或法线/环境辅助贴图 |

贴图用途是基于 SPIR-V 采样位置、格式和采样方式推断的，原始资源名仅为 `resXX`，没有引擎级语义名。

## 4. 片元着色器主流程

### 4.1 视线方向与实例矩阵

片元输入 `_input1` 作为世界空间位置，shader 用 `uniforms17._child11.xyz` 近似相机位置计算 view vector，并通过 `uniforms17._child26.w` 在相机方向和某个全局方向之间混合：

```text
float3 V0 = camera_pos - world_pos;
float3 V1 = uniforms17._child0[2].xyz;
float3 V  = Normalize(mix(V0, V1, uniforms17._child26.w));
```

shader 还使用 flat instance index 从 `uniforms20` 或 `ssbo30` 中取 object basis，用于把若干全局方向转换到物体/面部空间。

### 4.2 主色与表情/overlay atlas

主 albedo 来自 `res57`：

```text
base = Sample(res57, uv, bias).rgb * uniforms47._child24.rgb;
alpha = Sample(res57, uv).a;
```

随后根据 `uniforms47._child32` 选择一个 2x2 atlas 单元，从 `res50` 采样 overlay：

```text
tile = float2(fmod(index, 2) * 0.5, floor(index * 0.5) * 0.5);
overlayUV = tile + uv * 0.5;
overlay = Sample(res50, overlayUV);
base = mix(base, overlay.rgb, overlay.a * uniforms47._child33);
```

这通常用于脸部表情、腮红、妆容或区域变体。混合后 shader 执行近似 linear-to-sRGB 的 OETF：

```text
low  = color * 12.92;
high = pow(abs(color), 1.0 / 2.4) * 1.055 - 0.055;
srgb = clamp(select(color <= 0.0031, low, high), 0, 1);
```

### 4.3 2D LUT / Ramp 映射

`res51` 是 1024x32 `BC7_SRGB`，shader 用 RGB 中的 B 分量选择横向 slice，并对相邻 slice 做线性插值：

```text
z = srgb.b * 31;
slice = floor(z);
lutUV0 = srgb.rg * float2(0.0010, 0.0313) + float2(0.0005, 0.0156);
lutUV0.x += slice * 0.0313;
lut0 = SampleLod(res51, lutUV0, 0).rgb;
lut1 = SampleLod(res51, lutUV0 + float2(0.0313, 0), 0).rgb;
lutColor = mix(lut0, lut1, frac(z));
```

这个 LUT 会把基础肤色/风格化颜色映射到更符合角色面部的色阶。由于高度为 32 且横向分 32 个 slice，它本质上是一个被铺平成 2D 的 3D LUT 或 toon color ramp。

### 4.4 Mask、法线与 TBN

`res61` 使用主 UV 采样，其四通道被拆成 `_522/_523/_524/_525`。后续逻辑中：

- `_522` 与 `NdotL`、高度/朝向项相乘，像主面部阴影或受光 mask。
- `_523` 多次用于在皮肤/普通材质、面部阴影强弱、roughness/边缘项之间插值。
- `_524` 参与脸部高度或区域混合。
- `_525` 参与背光/边缘光或风格化高光的强度。

法线使用顶点法线 `_input2`、切线 `_input3.xyz` 和 handedness `_input3.w` 构建 TBN：

```text
N = normalize(_input2);
T = _input3.xyz;
B = cross(N, T) * _input3.w;
TBN = float3x3(T, B, N);
```

后续在局部构造圆形/球形法线细节，例如：

```text
xy = mask.xy * 2 - 1;
z = sqrt(1 - clamp(dot(xy, xy), 0, 1));
detailN = normalize(TBN * float3(xy, z));
```

这类逻辑常用于面部高光、眼下/鼻梁/脸颊区域的风格化法线，而不是传统 PBR 法线贴图的简单 unpack。

### 4.5 体积 LUT / 环境探针

shader 对 `res40`、`res41`、`res42`、`res43`、`res44`、`res45` 等 3D 纹理做多组采样，并用不同空间尺度判断是否在体积范围内：

- 先用世界位置缩放，如 `worldPos * 2.0`、`*0.5` 或其他比例。
- 对坐标做 `floor/fract`。
- 对边界距离做 `FClamp`，超出体积时降低权重。
- 在 3D 纹理 y 方向以 `1/3` 分段取三次样本，重建三通道方向或 SH-like 信息。

可见的典型模式：

```text
coord = fract(worldPos * scale);
volumeMask = clamp(boundaryDistance, 0, 1);
sample0 = SampleLod(volumeA, coord, 0);
sample1 = SampleLod(volumeB, float3(coord.x, coord.y/3 + 0/3, coord.z), 0);
sample2 = SampleLod(volumeB, float3(coord.x, coord.y/3 + 1/3, coord.z), 0);
sample3 = SampleLod(volumeB, float3(coord.x, coord.y/3 + 2/3, coord.z), 0);
dirOrIrradiance = sample1.rgb * 4 - 2;
```

这更像是局部光照探针、体积光照或环境 irradiance 数据，而不是角色自身贴图。

### 4.6 面部风格化阴影

片元阶段有大量 `SmoothStep`、`FClamp`、`FMix` 和 dot product 组合，核心输入包括：

- 面部/世界法线 `N`。
- 视线方向 `V`。
- 主光或风格化方向 `L`。
- mask 通道 `_522/_523/_524/_525`。
- `res48` 1D ramp。
- `res59` 1024x1024 ramp/mask 贴图。

关键片段显示 `res48` 使用 `float2(shadeCoord, 0.5)` 查表：

```text
shadeCoord = dot(faceDir, lightDir) * 0.5 + 0.5;
faceRamp = SampleLod(res48, float2(shadeCoord, 0.5), 0);
```

同时存在大量面部轮廓/上下方向修正，例如：

```text
front = clamp(dot(N, L) * 0.85 + 0.15, 0, 1);
faceMask = mask.r * mix(clamp(faceDir.y + 0.5, 0, 1), 1, mask.g);
shadow = smoothstep(edge0, edge1, value);
```

这说明脸部不是标准 Lambert/PBR 漫反射，而是经过 ramp 和 mask 强控制的二次元/卡通脸部阴影模型。

### 4.7 直接光、阴影与高光

shader 在约 1926 行进入一个最多 8 次的 light loop：

```text
while(true) {
  bool active = lightIndex <= 7;
  if(!active) break;
  ...
  while(true) {
    bool hasLight = maskBits != 0;
    if(!hasLight) break;
```

loop 中结合 `uniforms36`、`uniforms65`、`ssbo28` 和 `res37` 做光源选择与阴影。阴影使用 depth compare：

```text
shadow = ImageSampleDrefExplicitLod(res37, shadowUV, compareDepth, Lod(0));
```

局部光照逻辑包含：

- 点/聚光或方向光分支。
- 距离衰减和范围裁剪。
- shadow atlas 采样。
- toon diffuse mask。
- specular 近似项。

高光项可见 GGX/微表面风格公式特征：

```text
H = normalize(L + V);
NoH = dot(N, H);
a2 = roughness * roughness;
D = a2 / ((NoH * a2 - NoH) * NoH + 1)^2;
spec = clamp(D * 0.5 / (NoL + ... + 0.0001), 0, 20);
```

但最终高光仍会被 mask、ramp 和面部方向项强烈调制，因此视觉上更接近风格化角色脸部高光，而不是纯 PBR。

### 4.8 雾/大气合成

末尾阶段在得到主颜色后，根据 view distance、相机高度和若干全局参数计算雾/大气：

```text
transmittance = exp(-density * distance);
fogAmount = clamp(..., 0, 1);
fogColor = sky_or_atmosphere_color * (1 - fogAmount) + directional_scatter;
color = color * transmittance + fogColor;
```

如果 `uniforms17._child77.z > 0`，会走另一条带 3D texture `res68` 的分支：

```text
volumeFog = SampleLod(res68, fogCoord, 0);
color = volumeFog.rgb + atmosphere * volumeFog.a;
```

最终输出：

```text
*_14 = finalColorAndAlpha; // RT0, HDR color
*_15 = _1448;              // RT1, auxiliary/GBuffer-like data
```

## 5. GBuffer / MRT 输出解释

### RT0 (`ResourceId::33297`, `R11G11B10_FLOAT`)

`*_14` 接收 `_3168`。它来自：

1. 基础肤色、overlay 和 LUT/ramp 后的 base color。
2. 面部 mask 控制的风格化明暗。
3. 体积/环境光照。
4. 最多 8 组直接光和阴影。
5. GGX-like 高光和面部特殊 rim/edge 项。
6. 雾/大气颜色混合。

alpha 大多保持为前面构造的 material alpha，最终写 `float4(finalRGB, alpha)`。

### RT1 (`ResourceId::33259`, `R10G10B10A2_UNORM`)

`*_15` 接收 `_1448`。构造过程使用 `_input4/_input5` 的屏幕/投影坐标差异：

```text
float2 a = _input4.xy / max(_input4.z, 0);
float2 b = _input5.xy / max(_input5.z, 0);
float2 delta = a - b;
float2 packed = delta * 0.5 + 0.5;
alpha = FrontFacing ? 0.7 : 0.4;
out1 = float4(packed, 1.0, alpha);
```

因此 RT1 更像是运动/辅助向量、边缘/材质分类或后处理用 GBuffer，而不是传统 albedo/normal GBuffer。

## 6. 关键算法总结

- **压缩法线解码**：顶点阶段从 float bit pattern 中拆 10-bit 分量，并通过 octahedral decode 恢复单位法线。
- **蒙皮**：按实例数据读取骨骼矩阵，支持 2 或 4 bone 权重混合，同时变换位置、法线和切线。
- **面部主色**：`res57` 主贴图乘材质色，再叠加 `res50` 的 2x2 atlas overlay。
- **颜色 LUT**：`res51` 作为 32-slice 2D LUT，把基础颜色映射到风格化肤色。
- **面部阴影**：不是纯 Lambert，而是由方向、mask、ramp、smoothstep 阈值共同塑形。
- **直接光和阴影**：最多 8 组光源循环，使用 `res37` depth compare 做阴影，并叠加 GGX-like 高光。
- **体积/环境光**：多张 3D 纹理提供局部体积光照、探针或大气相关数据。
- **输出**：RT0 为 HDR 颜色，RT1 为辅助 GBuffer/后处理数据。

## 7. 注意事项

- SPIR-V 反编译没有原始变量名，`_childN` 和 `resXX` 只是 RenderDoc 生成名。本文中的“主贴图”“ramp”“mask”“体积光照”等是根据采样方式、格式、尺寸和使用位置推断。
- MCP 对该捕获的 cbuffer contents 返回大量 0，可能是读取偏移、动态 UBO 或 capture replay 限制导致；本文没有把这些 0 当作真实材质参数。
- 若后续继续分析皮肤、眼睛、头发等 draw，建议复用本文结构，但不要直接复用资源语义，因为不同 EID 可能绑定相同 shader 变体或不同 material 参数。
