# LuoXi 衣服渲染 Shader 分析

## 1. Draw 与管线概况

分析对象来自 `D:\ProgramData\RenderXtData\LuoXi.rdc` 中 5 个衣服相关 draw：

| EID | 部件 | Draw | Index count | VS | PS | Texture count |
|---:|---|---|---:|---|---|---:|
| 965 | 衣服渲染01，披风上的部件 | `vkCmdDrawIndexed()` | 14958 | `ResourceId::40478` | `ResourceId::40479` | 20 |
| 970 | 衣服渲染02，靴子以及全身挂件饰品 | `vkCmdDrawIndexed()` | 59679 | `ResourceId::40478` | `ResourceId::40479` | 20 |
| 999 | 衣服渲染03，衣服飘带 | `vkCmdDrawIndexed()` | 888 | `ResourceId::40524` | `ResourceId::40525` | 19 |
| 1004 | 衣服渲染04，披风 | `vkCmdDrawIndexed()` | 14280 | `ResourceId::40478` | `ResourceId::40479` | 20 |
| 1008 | 衣服渲染05，衣服本体 | `vkCmdDrawIndexed()` | 46845 | `ResourceId::40478` | `ResourceId::40479` | 20 |

5 个 draw 都属于同一角色主渲染/MRT pass：

| 项目 | 值 |
|---|---|
| Topology | `TriangleStrip` |
| Instance count | 1 |
| RT0 | `ResourceId::33297`, 2560x1440, `R11G11B10_FLOAT` |
| RT1 | `ResourceId::33259`, 2560x1440, `R10G10B10A2_UNORM` |
| Depth/stencil | `ResourceId::33254`, 2560x1440, `D32S8` |
| Shadow depth | `ResourceId::7641`, 6144x4096, `D16` |

这组衣服 shader 在该 pass 中实现的主要效果包括：

| 效果 | 实现方式概述 |
|---|---|
| 骨骼蒙皮与实例变换 | VS 从实例 uniform 和 `ssbo29` 读取矩阵，按骨骼索引/权重变换位置、法线和切线。 |
| 压缩法线/切线解码 | VS 从打包 float/uint 中拆 10-bit 分量，执行 octahedral decode，并重建切线空间。 |
| 衣服材质基础色 | PS 采样 2048x2048 `BC7_SRGB` base color，并乘材质色参数。 |
| 法线与材质 mask | PS 采样 `BC5_UNORM` 法线和 `BC7/R8G8B8A8_UNORM` mask/参数贴图，控制布料、饰品、披风等不同材质响应。 |
| 风格化明暗 | 使用 256x1 ramp、mask 通道和 `SmoothStep/FMIX` 阈值塑形，不是纯 Lambert/PBR 明暗。 |
| 环境反射 / IBL | 使用 `BC6_UFLOAT` cube map 按反射方向和 roughness-like LOD 采样。 |
| 体积/探针光照 | 多张 3D 纹理按世界位置采样，用于局部体积光照、环境 irradiance 或探针数据。 |
| 动态光与阴影 | PS 最多遍历 8 组光源，使用 `D16` shadow map 做 depth compare 和 PCF-like 多点阴影采样。 |
| 雾与大气合成 | 最终颜色叠加 view distance、相机高度和 3D LUT/体积雾相关项。 |
| 双 MRT 输出 | RT0 写 HDR 颜色，RT1 写 packed 辅助/GBuffer-like 数据供后处理或分类使用。 |

RenderDoc MCP 对该捕获的固定功能状态读取不完整，blend/depth/stencil/rasterizer 细节未能完整返回。本文主要依据 draw state、shader reflection、资源绑定和 SPIR-V 反编译片段分析。

## 2. Shader 分组

### 2.1 主衣服材质组：EID 965 / 970 / 1004 / 1008

这 4 个 draw 共用同一套 shader：

- VS：`ResourceId::40478`
- PS：`ResourceId::40479`
- VS 反编译附录：`doc/附录/LuoXi_cloth_EID965_970_1004_1008_vs_spirv.txt`
- PS 反编译附录：`doc/附录/LuoXi_cloth_EID965_970_1004_1008_ps_spirv.txt`
- PS 反编译行数约 3676
- 片元阶段绑定 20 张贴图

区别主要来自材质贴图资源：

- EID 965 与 970 使用同一组衣服/饰品贴图，包含 `ResourceId::74801`、`ResourceId::74812`、`ResourceId::74785`。
- EID 1004 与 1008 使用另一组披风/衣服本体贴图，包含 `ResourceId::47560`、`ResourceId::47662`、`ResourceId::47454`。
- 这 4 个 draw 的全局屏幕、阴影、体积/探针、ramp 和环境辅助贴图基本一致。

### 2.2 飘带材质组：EID 999

EID 999 使用另一套 shader：

- VS：`ResourceId::40524`
- PS：`ResourceId::40525`
- VS 反编译附录：`doc/附录/LuoXi_cloth_EID999_vs_spirv.txt`
- PS 反编译附录：`doc/附录/LuoXi_cloth_EID999_ps_spirv.txt`
- PS 反编译行数约 3669
- 片元阶段绑定 19 张贴图

飘带 shader 的结构与主衣服材质高度相似：仍是同类顶点蒙皮、同类主色/材质采样、同类环境/阴影/光源循环和双 MRT 输出。主要差异是贴图集合更少，且材质贴图资源换成 `ResourceId::47454`、`ResourceId::47662`、`ResourceId::31016` 等。

## 3. 顶点着色器

### 3.1 输入布局

主衣服材质组 EID 965 的顶点输入：

| 输入 | VB | Offset | Format | 推断用途 |
|---|---:|---:|---|---|
| `_input0` | 0 | 0 | `R32G32B32_FLOAT` | 位置 |
| `_input2` | 0 | 12 | `R32_FLOAT` | 压缩法线/附加打包数据 |
| `_input3` | 4 | 12 | `R8G8B8A8_UNORM` | 顶点颜色或 mask |
| `_input1` | 1 | 4 | `R32G32_FLOAT` | 主 UV |
| `_input4` | 1 | 20 | `R8G8B8A8_SNORM` | 切线/符号或压缩切线 |
| `_input5` | 3 | 0 | `R32G32B32_FLOAT` | 备用位置/形变位置 |
| `_input6` | 3 | 12 | `R32_FLOAT` | 压缩法线/形变数据 |
| `_input8` | 2 | 0 | `R16G16B16A16_UNORM` | 骨骼权重 |
| `_input9` | 2 | 8 | `R8G8B8A8_UINT` | 骨骼索引 |

EID 999 飘带输入签名相同，但 VB1 打包不同：

| 差异项 | 主衣服组 EID 965 | 飘带 EID 999 |
|---|---|---|
| VB1 stride | 24 | 12 |
| `_input1` offset | 4 | 0 |
| `_input4` offset | 20 | 8 |

这个差异说明飘带网格的 UV/切线数据打包更紧凑，但 shader 入口语义保持一致。

### 3.2 常量与 buffer

两套 VS 使用同类资源：

| 资源 | Bind point | 大小 | 用途推断 |
|---|---:|---:|---|
| `uniforms24` | 14 | 4512 | frame/view 矩阵、相机、屏幕、环境参数 |
| `uniforms31` | 0 | 336 | material/object 小参数块，含 UV transform |
| `uniforms27` | 0 | 65536 | 256 个 instance/object 结构 |
| `ssbo29` | 16 | buffer | 骨骼或形变矩阵数组 |

### 3.3 主要逻辑

衣服 VS 与脸部 VS 的骨架一致，但 material 参数块尺寸从脸部的 384 bytes 变为 336 bytes：

1. **压缩法线/切线解码**  
   `_input2.x` 和 `_input6.x` 被 `Bitcast` 为 `uint`，按 10-bit 分量拆解，乘 `0.002` 映射到约 `[-1, 1]` 后执行 octahedral normal decode。随后用 `Normalize` 得到世界变换前的法线/辅助方向。

   关键反编译片段：

   ```text
   float _122 = _114.x;
   uint _123 = Bitcast(_122);
   uint _124 = _123 & 1073741824;
   ...
   float _202 = _118.x;
   uint _203 = Bitcast(_202);
   uint _204 = _203 << 22;
   uint _205 = _204 >> 22;
   ```

2. **骨骼蒙皮**  
   shader 根据 `_InstanceIndex` 从 `uniforms27` 读取实例数据。如果实例 flag 表示需要蒙皮，则用 `_input9` 骨骼索引和 `_input8` 权重从 `ssbo29` 读取 3 行矩阵并做 2 bone 或 4 bone 加权。位置、法线和切线都会经过同一组骨骼/实例变换。

3. **世界空间输出**  
   VS 输出包括 UV、世界空间位置、法线、切线、屏幕/投影相关坐标、附加方向和 flat instance id。片元阶段用这些 varyings 构造 TBN、视线方向、屏幕辅助输出和光照输入。

4. **MRT 辅助坐标准备**  
   VS 输出的 `_output4/_output5` 在 PS 中参与 RT1 的 packed XY 构造。这个输出与 motion vector、边缘/描边或后处理分类数据相关。

## 4. 片元资源绑定

### 4.1 共享全局资源

5 个衣服 draw 都绑定以下全局资源或同类资源：

| Slot | 资源 | 格式 | 用途推断 |
|---:|---|---|---|
| 0 | `ResourceId::33224`, 2560x1440 `R8G8_UNORM` | 屏幕空间 buffer，参与环境/遮蔽/深度相关修正 |
| 1 | `ResourceId::7641`, 6144x4096 `D16` | shadow map，PS 中使用 `ImageSampleDrefExplicitLod` |
| 2 | `ResourceId::14387`, 4x4 `R8G8B8A8_SRGB` | fallback 小贴图或 LUT |
| 3-5 | `ResourceId::13449/13433/13422`, 128x192 `R8G8B8A8_UNORM` | 体积 LUT/局部光照探针 |
| 6-8 | `ResourceId::13442/13425/13419`, 128x64 `R11G11B10_FLOAT` | HDR 体积光照/探针 |
| 9 | `ResourceId::65948`, 1x1 `R8G8B8A8_SRGB` | fallback 3D LUT/体积雾 |
| 10 | `ResourceId::4771`, 256x256 `BC7_UNORM` | 公共材质/环境辅助贴图 |
| 11 | `ResourceId::642`, 1024x1024 `BC7_UNORM` | 公共 mask/材质贴图 |
| 12 | `ResourceId::12357`, 128x128 `BC6_UFLOAT` | cube map，反射或 IBL |
| 15/14 | `ResourceId::58841`, 256x1 `R8G8B8A8_UNORM` | ramp/toon shade 或 BRDF ramp |
| 18/17 | `ResourceId::4645`, 512x512 `BC3_SRGB` | triplanar/环境颜色辅助 |
| 19/18 | `ResourceId::660`, 1024x1024 `BC7_UNORM` | triplanar/环境材质辅助 |

`ResourceId::12357` 在 reflection 中类型为 `TextureCube`，PS 反编译中以反射方向和显式 LOD 采样，用途更接近环境反射或 IBL。

### 4.2 EID 965 / 970 材质贴图

| Slot | 名称 | ResourceId | 格式 | 推断用途 |
|---:|---|---|---|---|
| 13 | `res49` | `ResourceId::4692` | 256x256 `R8G8B8A8_UNORM` | 材质 mask / 参数贴图 |
| 14 | `res56` | `ResourceId::74801` | 2048x2048 `BC7_UNORM` | 衣服/饰品材质 mask 或 ORM |
| 16 | `res58` | `ResourceId::74812` | 2048x2048 `BC5_UNORM` | 法线贴图 |
| 17 | `res54` | `ResourceId::74785` | 2048x2048 `BC7_SRGB` | base color / albedo |

EID 965 与 970 的贴图资源完全一致，说明这两类部件使用同一个材质实例或同一套 atlas。

### 4.3 EID 1004 / 1008 材质贴图

| Slot | 名称 | ResourceId | 格式 | 推断用途 |
|---:|---|---|---|---|
| 13 | `res49` | `ResourceId::4692` | 256x256 `R8G8B8A8_UNORM` | 材质 mask / 参数贴图 |
| 14 | `res56` | `ResourceId::47560` | 2048x2048 `BC7_UNORM` | 披风/衣服本体 mask 或 ORM |
| 16 | `res58` | `ResourceId::47662` | 2048x2048 `BC5_UNORM` | 法线贴图 |
| 17 | `res54` | `ResourceId::47454` | 2048x2048 `BC7_SRGB` | base color / albedo |

EID 1004 与 1008 共享这一组贴图，说明披风和衣服本体也使用同一材质 atlas 或同一 shader 参数布局。

### 4.4 EID 999 飘带材质贴图

| Slot | 名称 | ResourceId | 格式 | 推断用途 |
|---:|---|---|---|---|
| 13 | `res49` | `ResourceId::31016` | 256x256 `R8G8B8A8_UNORM` | 飘带材质 mask / 参数贴图 |
| 15 | `res56` | `ResourceId::47662` | 2048x2048 `BC5_UNORM` | 法线贴图 |
| 16 | `res54` | `ResourceId::47454` | 2048x2048 `BC7_SRGB` | base color / albedo |

飘带少绑定一张 2048x2048 `BC7_UNORM` mask/ORM 贴图，说明材质参数更简单，或对应参数被打包进其他贴图。

## 5. 片元着色器逻辑

### 5.1 主色、mask 和材质参数

PS 开始阶段先用主 UV 采样 base color，并乘材质色：

```text
Image<float, 2D> _480 = *_54;
float4 _485 = ImageSampleImplicitLod(_484, uv, Bias(lodBias));
float3 base = _485.xyz * uniforms47._child24.xyz;
```

随后采样材质参数贴图和法线贴图：

```text
float4 mask = Sample(res56, uv);
float4 nrm  = Sample(res58, uv);
```

其中 `BC5_UNORM` 贴图通常提供 tangent-space normal 的 xy，shader 会重建 z 并通过 TBN 转到世界空间。`BC7_UNORM` 或 `R8G8B8A8_UNORM` 贴图通道则参与 roughness、metallic、AO、toon mask 或材质分区。由于原始资源没有语义名，具体通道含义只能从采样后的混合位置推断。

### 5.2 Triplanar / 环境辅助采样

反编译中多次对同一 2D 贴图以 `xz`、`xy`、`zy` 三组坐标采样，再按法线权重混合：

```text
float4 sxz = Sample(tex, worldOrLocal.xz);
float4 sxy = Sample(tex, worldOrLocal.xy);
float4 szy = Sample(tex, worldOrLocal.zy);
float4 tri = sxz * weight.y + sxy * weight.z + szy * weight.x;
```

这种模式出现在 `res50`、`res51`、`res52`、`res53` 等资源上，推断用于环境污渍/布料细节、局部渐变、间接光照或材质宏观变化，而不是简单 UV 贴图。

### 5.3 体积光照 / 探针

衣服 PS 与脸部 PS 一样采样多组 3D 纹理。逻辑模式为：

1. 用世界位置乘缩放得到体积坐标。
2. 使用 `floor/fract` 得到局部体素坐标。
3. 通过边界距离计算权重。
4. 在 y 方向按 `1/3` 分段采样三次，重建方向或 SH-like 颜色。

关键片段：

```text
float3 coord = fract(worldPos * scale);
float4 probe0 = SampleLod(res40, coord, 0);
float4 a = SampleLod(res41, float3(coord.x, coord.y / 3 + 0/3, coord.z), 0);
float4 b = SampleLod(res41, float3(coord.x, coord.y / 3 + 1/3, coord.z), 0);
float4 c = SampleLod(res41, float3(coord.x, coord.y / 3 + 2/3, coord.z), 0);
float3 dirOrIrr = a.rgb * 4 - 2;
```

这更接近局部体积光照、light probe 或环境 irradiance 数据。

### 5.4 Ramp / 风格化明暗

`ResourceId::58841` 是 256x1 `R8G8B8A8_UNORM`，PS 中用 `float2(shadeCoord, 0.5)` 显式 LOD 采样：

```text
float2 rampUV = float2(shadeCoord + 0.5, 0.5);
float4 ramp = SampleLod(res48, rampUV, 0);
```

该 ramp 与直接光、法线方向、mask 通道共同控制明暗过渡。衣服材质因此不是纯 PBR；它在 PBR-like 高光和阴影基础上加入了 toon/ramp 风格化控制。

### 5.5 Cube map 反射 / IBL

主衣服组 reflection 中 `res61` 类型为 `TextureCube`，EID 999 中对应 `res59`。PS 使用反射方向和显式 LOD 采样：

```text
Image<float, Cube> cube = *res61;
float lod = roughnessTerm * 1.2 + 5.0;
float4 ibl = ImageSampleExplicitLod(cube, reflectDir, Lod(lod));
```

这说明衣服材质包含环境反射或 specular IBL。LOD 随 roughness/材质参数变化，符合基于粗糙度选择预滤波环境贴图的做法。

### 5.6 光源循环与阴影

PS 存在最多 8 次的外层光源循环，并包含按 bit mask 遍历有效光源的内层循环：

```text
while(true) {
  bool active = lightIndex <= 7;
  if(!active) break;
  ...
  while(true) {
    bool hasLight = lightMask != 0;
    if(!hasLight) break;
```

阴影采样使用 `ResourceId::7641` 的 D16 shadow map，并采用 depth compare：

```text
float shadow = ImageSampleDrefExplicitLod(shadowMap, shadowUV, compareDepth, Lod(0));
```

反编译显示 PCF-like 多点采样：同一 shadow map 被多次以不同 offset 采样后按权重累加。这部分用于直接光阴影。

### 5.7 高光和材质响应

shader 中存在 `Pow`、`Dot`、`FClamp`、`Normalize` 等高光计算。部分逻辑类似微表面/GGX 或 stylized specular：

```text
float3 H = normalize(L + V);
float NoH = clamp(dot(N, H), 0, 1);
float spec = pow(1 - clamp(dot(...), 0, 1), exponent) * intensity;
```

同时 cube map IBL、ramp、mask、roughness/metallic 参数会共同调制最终高光。衣服和饰品中金属挂件、靴子硬质部分、披风布料应通过材质 mask 区分不同响应。

### 5.8 透明与裁剪

对 `Discard` 的搜索没有命中，说明这 5 个衣服 draw 不使用片元丢弃式 alpha test。飘带 EID 999 也没有发现 `Discard`。若存在透明/半透明控制，更可能通过后续混合状态、alpha 输出或材质 mask 实现；但固定功能 blend 状态本次 MCP 未能完整读取。

### 5.9 雾 / 大气合成

末尾阶段与脸部 shader 相似，使用 view distance、相机高度和全局大气参数计算雾/散射，并在特定分支采样 fallback 3D LUT：

```text
float fogWeight = clamp((viewDepth - fogStart) * scale, 0, 1);
float4 volumeFog = SampleLod(res67_or_res65, fogCoord, 0);
finalColor = finalColor * transmittance + fogOrAtmosphere;
```

最终写入 RT0。

## 6. MRT 输出

### 6.1 RT0：HDR color

主衣服组最终输出：

```text
*_14 = _3626;
```

飘带组最终输出：

```text
*_14 = _3619;
```

RT0 是 `R11G11B10_FLOAT`，承载经过以下步骤合成后的 HDR 颜色：

- base color 和材质色
- 法线/roughness/metallic/mask 参与的材质响应
- 体积/探针间接光照
- cube map IBL
- ramp/toon 风格化明暗
- 最多 8 组直接光和 shadow map 阴影
- 雾/大气项

### 6.2 RT1：辅助 GBuffer-like 输出

主衣服组 RT1 写 `_1953`：

```text
float2 packed = value + float2(0.5, 0.5);
float alpha = condition > 0.1 ? 0.7 : 0.4;
_1953 = float4(packed.x, packed.y, 1.0, alpha);
*_15 = _1953;
```

飘带组 RT1 写 `_1947`，构造完全同类：

```text
float2 packed = value + float2(0.5, 0.5);
float alpha = condition > 0.1 ? 0.7 : 0.4;
_1947 = float4(packed.x, packed.y, 1.0, alpha);
*_15 = _1947;
```

因此 RT1 不是传统 albedo/normal GBuffer，更像后处理用辅助向量、边缘/材质分类或 motion-like 数据。alpha 的 `0.7/0.4` 分类值与脸部 draw 的 RT1 输出模式一致。

## 7. 关键结论

- 5 个衣服 draw 都在同一角色 MRT pass 中渲染，输出 RT0/RT1 和深度目标完全一致。
- EID 965/970/1004/1008 共用主衣服 shader；EID 999 飘带 shader 是同结构变体。
- 顶点阶段支持骨骼蒙皮、压缩法线/切线解码和实例矩阵变换。
- 片元阶段是 stylized PBR：有 base color、BC5 normal、mask/ORM、ramp、cube map IBL、体积探针、直接光、PCF-like shadow 和雾/大气。
- 未发现 `Discard`，这批衣服 draw 不像 alpha test/cutout pass。
- RT1 输出是统一的辅助/GBuffer-like 数据格式，和脸部 draw 的输出形态一致。

## 8. 注意事项

- `resXX` 和 `_childN` 是 RenderDoc 生成名，没有原始引擎变量名。本文的 base color、normal、mask、ORM、ramp、IBL 等用途均按格式、尺寸、采样方式和使用位置推断。
- MCP 对固定功能状态和 cbuffer 实际数值读取有限，本文不把未能验证的 cbuffer 数值作为参数结论。
- 若后续继续分析武器、毛发或头发，建议复用本文件的“shader 分组 + 材质实例差异”结构，因为角色主体多个 draw 明显复用相近的渲染框架。
