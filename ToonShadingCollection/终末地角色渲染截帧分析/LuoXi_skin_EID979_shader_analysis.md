# LuoXi 皮肤渲染 Shader 分析：EID 979

本文分析 `D:\ProgramData\RenderXtData\LuoXi.rdc` 中角色皮肤 draw：

| 项目 | 值 |
|---|---|
| Draw | EID 979, `vkCmdDrawIndexed(23220, 1)` |
| API | Vulkan |
| Topology | `TriangleStrip` |
| Instance count | 1 |
| Vertex shader | `ResourceId::40482`, entry `main` |
| Pixel shader | `ResourceId::40483`, entry `main` |
| RT0 | `ResourceId::33297`, 2560x1440, `R11G11B10_FLOAT` |
| RT1 | `ResourceId::33259`, 2560x1440, `R10G10B10A2_UNORM` |
| Depth/stencil | `ResourceId::33254`, 2560x1440, `D32S8` |
| VS 反编译附录 | `doc/附录/LuoXi_skin_EID979_vs_spirv.txt` |
| PS 反编译附录 | `doc/附录/LuoXi_skin_EID979_ps_spirv.txt` |

RenderDoc MCP 对该 capture 的固定管线状态读取存在接口限制，blend/depth/stencil/rasterizer 没有完整返回。本文主要依据 draw state、shader reflection、资源绑定、顶点输入布局和 SPIR-V 反编译进行分析。凡是资源用途或变量语义无法从调试名直接确认的地方，均标为“推断”。

## 1. 管线和输出

该 draw 属于角色主渲染 pass 的皮肤材质。片元 shader 不只是写入传统 GBuffer，而是在同一个 pass 内完成 base color、法线重建、体积/环境光照采样、风格化皮肤混合、动态光和阴影、雾/大气合成，最后写两个 MRT。

| 输出 | 格式 | 观测范围 | 推断用途 |
|---|---|---|---|
| RT0 | `R11G11B10_FLOAT` | min `(0,0,0,1)`, max about `(5.25,5.5,6.875,1)` | HDR 颜色输出或主 lighting target |
| RT1 | `R10G10B10A2_UNORM` | RGB 约 `0.44-1.0`, A 最大 `0.3333` | packed 辅助/GBuffer-like 信息，供后处理、描边、分类或 motion-like 逻辑使用 |

片元反编译末尾明确写出：

```text
*_14 = _3415;   // Location(0), RT0
*_15 = _1809;   // Location(1), RT1
```

## 2. 顶点 Shader

### 2.1 输入布局

| 输入 | VB | Offset | Format | 推断用途 |
|---|---:|---:|---|---|
| `_input0` | 0 | 0 | `R32G32B32_FLOAT` | position |
| `_input2` | 0 | 12 | `R32_FLOAT` | packed normal / tangent helper |
| `_input3` | 4 | 12 | `R8G8B8A8_UNORM` | vertex color 或 material mask |
| `_input1` | 1 | 0 | `R32G32_FLOAT` | UV |
| `_input4` | 1 | 8 | `R8G8B8A8_SNORM` | tangent 或 handedness |
| `_input5` | 3 | 0 | `R32G32B32_FLOAT` | alternate/deformed position |
| `_input6` | 3 | 12 | `R32_FLOAT` | alternate packed normal |
| `_input8` | 2 | 0 | `R16G16B16A16_UNORM` | skin weights |
| `_input9` | 2 | 8 | `R8G8B8A8_UINT` | bone indices |
| `_InstanceIndex` | built-in | - | `UInt` | instance/object index |

索引 buffer 与多个 vertex buffer 均来自 `ResourceId::29635`，另有 VB4 `ResourceId::135`。VS 还读取 `ssbo29`/binding 16，用于骨骼或形变矩阵数据。

### 2.2 常量和 buffer

| 资源 | Descriptor | 大小 | 推断用途 |
|---|---|---:|---|
| `uniforms24` | set 0 binding 14 | 4512 | frame/view 矩阵、相机、屏幕、环境参数 |
| `uniforms31` | set 1 binding 0 | 368 | material/object 小参数块，含 UV transform |
| `uniforms27` | set 2 binding 0 | 65536 | 256 个 instance/object 结构 |
| `ssbo29` | set 0 binding 16 | buffer | 骨骼/形变矩阵数组 |

### 2.3 主要逻辑

**压缩法线/切线解码。** VS 将 `_input2.x` 和 `_input6.x` bitcast 为 `uint` 后拆 10-bit 分量，并进行 octahedral normal decode。`0.002` 近似把 10-bit 有符号范围映射到 `[-1, 1]`。

```text
uint packed = Bitcast(_input2.x);
x = signed10(packed);
y = signed10(packed >> 10);
z_or_sign = signed10(packed >> 20);
n.xy = float2(x, y) * 0.002;
n.z = 1 - abs(n.x) - abs(n.y);
if (n.z < 0) n.xy = (1 - abs(n.yx)) * sign(n.xy);
n = normalize(n);
```

**切线基重建。** shader 以解码法线为基准构造两条正交方向，通过 `Cross`/`Normalize` 得到 tangent frame 辅助向量，再用 packed sign 恢复 tangent `w`。该逻辑用于后续片元阶段 TBN 重建。

**骨骼蒙皮。** shader 根据 `_InstanceIndex` 读取 `uniforms27` 当前 instance 结构，检查 object flag。如果需要蒙皮，则用 `_input9` 骨骼索引和 `_input8` 权重从 `ssbo29` 读取骨骼矩阵，支持 2-bone 和 4-bone blend。位置、法线、切线都会通过同一组骨骼/实例矩阵变换。

**空间变换与 varyings。** 蒙皮后的局部位置再乘 instance/world/view-projection 相关矩阵，并应用屏幕偏移修正，最终输出裁剪空间位置。VS 输出到 PS 的主要 varyings 为：

| VS 输出 | PS 输入 | 推断语义 |
|---|---|---|
| `_output0` | `_input0` | UV，已应用 material UV scale/bias |
| `_output1` | `_input1` | world position |
| `_output2` | `_input2` | world normal |
| `_output3` | `_input3` | world tangent + handedness |
| `_output4` | `_input4` | screen/clip helper |
| `_output5` | `_input5` | secondary projected helper |
| `_output6` | `_input6` | decoded auxiliary normal/direction |
| `_output7` | `_input7` | vertex color 或 material mask |
| `_output8` | `_input8` | flat instance index |

## 3. 片元 Shader 资源

### 3.1 常量与 buffer

| 资源 | Descriptor | 大小 | 推断用途 |
|---|---|---:|---|
| `uniforms34` | set 0 binding 12 | 32864 | light/cluster 或全局 light list 参数 |
| `uniforms36` | set 0 binding 13 | 11440 | 多光源、阴影矩阵、投影/区域光参数 |
| `uniforms17` | set 0 binding 14 | 4512 | frame/view、相机、环境、雾、大气、全局调制 |
| `uniforms32` | set 0 binding 38 | 48 | light loop / tile 控制参数 |
| `uniforms60` | set 0 binding 39 | 2560 | shadow/atlas 或投影矩阵数组 |
| `uniforms47` | set 1 binding 0 | 368 | 皮肤材质参数块 |
| `uniforms20` | set 2 binding 0 | 65536 | per-instance/object 数据 |
| `ssbo30` | set 0 binding 16 | buffer | object/骨骼/实例辅助数据 |
| `ssbo28` | set 0 binding 40 | buffer | light list 或 tile/cluster 索引 |

MCP 读出的部分 constant buffer 数值大量为 0，疑似受捕获或接口读取限制影响；本文只根据访问模式、格式和计算结构分析语义，不基于这些值做定量结论。

### 3.2 贴图绑定

| Slot | 名称 | ResourceId | Descriptor binding | 尺寸/格式 | 推断用途 |
|---:|---|---|---:|---|---|
| 0 | `res38` | `ResourceId::33224` | 18 | 2560x1440 `R8G8_UNORM` | 屏幕空间辅助 buffer，参与遮蔽/皮肤调制 |
| 1 | `res37` | `ResourceId::7641` | 20 | 6144x4096 `D16` | shadow depth，使用 depth compare |
| 2 | `res58` | `ResourceId::14387` | 22 | 4x4 `R8G8B8A8_SRGB` | 小型 LUT/默认贴图 |
| 3 | `res45` | `ResourceId::13449` | 23 | 128x192 `R8G8B8A8_UNORM` | 体积 probe/环境 LUT |
| 4 | `res43` | `ResourceId::13433` | 24 | 128x192 `R8G8B8A8_UNORM` | 体积 probe/环境 LUT |
| 5 | `res41` | `ResourceId::13422` | 25 | 128x192 `R8G8B8A8_UNORM` | 体积 probe/环境 LUT |
| 6 | `res44` | `ResourceId::13442` | 26 | 128x64 `R11G11B10_FLOAT` | HDR 体积光照/SH 权重 |
| 7 | `res42` | `ResourceId::13425` | 27 | 128x64 `R11G11B10_FLOAT` | HDR 体积光照/SH 权重 |
| 8 | `res40` | `ResourceId::13419` | 28 | 128x64 `R11G11B10_FLOAT` | HDR 体积光照/SH 权重 |
| 9 | `res63` | `ResourceId::65948` | 29 | 1x1 `R8G8B8A8_SRGB` | fallback 3D LUT |
| 10 | `res51` | `ResourceId::657` | 33 | 256x256 `BC7_UNORM` | 屏幕/方向 mask 或皮肤细节 |
| 11 | `res49` | `ResourceId::75359` | 4 | 1024x32 `BC7_SRGB` | 颜色校正/3D LUT atlas |
| 12 | `res48` | `ResourceId::57522` | 5 | 256x1 `R8G8B8A8_UNORM` | 1D ramp，皮肤色调/半透扩散曲线 |
| 13 | `res56` | `ResourceId::49140` | 6 | 1024x1024 `BC5_UNORM` | normal map |
| 14 | `res54` | `ResourceId::49251` | 7 | 1024x1024 `BC7_SRGB` | base color/albedo |
| 15 | `res53` | `ResourceId::4645` | 0 | 512x512 `BC3_SRGB` | tri-planar/环境颜色或细节贴图 |
| 16 | `res52` | `ResourceId::660` | 1 | 1024x1024 `BC7_UNORM` | tri-planar/细节 mask 或辅助法线 |

贴图用途是推断。RenderDoc 只提供 `resXX` 原始资源名，未包含引擎材质参数名。

## 4. 片元主流程

### 4.1 View vector 与 TBN

片元输入 `_input1` 是 world position。shader 从 `uniforms17._child11.xyz` 读取类似 camera position 的值，计算 view direction；同时从 `uniforms20`/`ssbo30` 根据 flat instance index 获取 object basis，用于局部方向、体积采样和投影相关计算。

TBN 使用 VS 输出的 normal、tangent 和 handedness 重建：

```text
N = normalize(_input2);
T = _input3.xyz;
B = cross(N, T) * _input3.w;
```

正反面通过 `FrontFacing` 调制法线方向，避免背面或镜像面上的法线方向错误。

### 4.2 Base color 与颜色 LUT

片元首先采样 `res54`：

```text
base = Sample(res54, uv, mipBias).rgb * uniforms47._child24.rgb;
alpha = Sample(res54, uv).a;
```

随后 shader 执行一段标准 sRGB OETF 形式的转换：

```text
low  = color * 12.92;
high = pow(abs(color), 1.0 / 2.4) * 1.055 - 0.055;
srgb = clamp(select(color <= 0.0031, low, high), 0, 1);
```

紧接着通过 `res49` 进行 3D LUT atlas 查询。SPIR-V 中的关键特征是把 RGB 映射到 32 层 atlas：

```text
zSlice = srgb.b * 31;
slice0 = floor(zSlice);
uvLut = srgb.rg * 31 * float2(0.0010, 0.0313) + float2(0.0005, 0.0156);
sample0 = SampleLod(res49, uvLut + slice0 * float2(0.0313, 0), 0);
sample1 = SampleLod(res49, uvLut + (slice0 + 1) * float2(0.0313, 0), 0);
baseLut = mix(sample0.rgb, sample1.rgb, frac(zSlice));
```

这说明皮肤颜色并非直接 albedo 输出，而是经过风格化颜色 LUT 或色调映射。`res49` 的 `1024x32` 尺寸也吻合 `32 x 32 x 32` 3D LUT 展开为 2D atlas 的模式。

### 4.3 Normal map

`res56` 是 `BC5_UNORM`，片元采样后用 `x` 和 `w`/`y` 通道重建切线空间法线，再乘材质法线强度：

```text
n.xy = sample.wy * 2 - 1;
n.z = sqrt(max(0, 1 - dot(n.xy, n.xy)));
n.xy *= uniforms47._child3;
N = normalize(TBN * n);
```

这是典型的双通道法线贴图重建。`BC5_UNORM` 格式也支持该判断。

### 4.4 体积光照 / Probe 采样

shader 对多组 3D 纹理进行 world position 采样。明显模式包括：

```text
coord = frac((worldPos * scale + 0.5) * uniforms17._child128.xyz);
weight = SampleLod(res40/res42/res44, coord, 0);
dir0 = SampleLod(res41/res43/res45, float3(coord.x, y * 1/3 + 0/3, coord.z), 0).xyz * 4 - 2;
dir1 = SampleLod(... y * 1/3 + 1/3 ...).xyz * 4 - 2;
dir2 = SampleLod(... y * 1/3 + 2/3 ...).xyz * 4 - 2;
```

这类代码把 3D 纹理的 Y 方向切成三段，分别解码三个方向向量或 SH-like 分量；`R11G11B10_FLOAT` 纹理提供 HDR 权重，`R8G8B8A8_UNORM` 纹理提供方向/系数。shader 根据世界位置落在哪个体积范围内，分层混合不同 scale 的 probe 数据。

推断：这是场景体积光照、local irradiance probe 或角色用环境光场，不是单纯材质贴图。

### 4.5 皮肤 ramp 与半透扩散

`res48` 为 256x1 `R8G8B8A8_UNORM`，片元以 `dot(N, L)` 或类似光照角度映射到 `0..1` 后采样：

```text
u = saturate(dotLike * 0.5 + 0.5);
skinRamp = SampleLod(res48, float2(u, 0.5), 0);
```

采样结果的 `rgb` 与 `a` 分别参与：

| ramp 通道 | 推断用途 |
|---|---|
| RGB | 皮肤色调、明暗边界或次表面散射颜色 |
| A | 与 base alpha / screen buffer 混合后控制皮肤软化、透光或暗部混合强度 |

该 draw 的皮肤逻辑更接近 stylized skin shading：通过 LUT/ramp 和多组 mask 控制肤色，不是纯 Cook-Torrance PBR。

### 4.6 屏幕空间辅助

`res38` 是 2560x1440 `R8G8_UNORM`，片元通过当前像素坐标 `FragCoord.xy` 做 `ImageFetch`：

```text
screen = ImageFetch(res38, int2(FragCoord.xy), 0);
occlusionOrMask = mix(1, screen.r, uniforms36._child6.x);
skinMix = screen.g;
```

这说明皮肤 shader 会读取一个前置屏幕 buffer。结合格式和使用方式，推断其用于屏幕空间遮蔽、角色/面部 mask、厚度或后处理分类辅助。

### 4.7 直接光、阴影和高光

片元 shader 中存在动态光循环和 shadow compare：

```text
shadow = ImageSampleDrefExplicitLod(res37, shadowUV, compareDepth, Lod(0));
```

反编译显示多次 depth compare 并加权累加，形成 PCF-like 阴影。直接光部分使用 roughness-like 参数构建微表面高光：

```text
H = normalize(L + V);
a2 = roughness * roughness;
D_like = a2 / ((dot(N,H) * a2 - dot(N,H)) * dot(N,H) + 1)^2;
V_like = 0.5 / (2 * NoL + roughness * (1 + NoL - NoL) + 0.0001);
spec = clamp(D_like * V_like - 0.0001, 0, 20) * specColor;
```

同时存在用于皮肤的额外方向偏移、ramp 混合和色度保持：

```text
luma = dot(color, float3(0.2127, 0.7152, 0.0722));
color = mix(luma.xxx, color, 1.2);
```

这类色度增强和亮度归一化用于保持皮肤在风格化光照中的色彩稳定。

### 4.8 雾和大气合成

shader 末尾存在基于视距、相机高度和指数衰减的雾/大气逻辑：

```text
transmittance = exp2(-densityIntegral);
fogColor = atmosphereColor * (1 - transmittance) + directionalFog;
final = litColor * transmittance + fogColor;
```

若 `uniforms17._child77.z` 大于 0，还会采样 `res63` 3D LUT/fallback volume，并通过视距映射到 `z` 层。这一段会在 RT0 输出前与皮肤 lighting 结果合成。

## 5. 关键算法总结

按执行顺序，EID 979 皮肤 shader 可以重建为以下高层伪代码：

```text
VS:
  decode packed normals/tangents
  read instance data by InstanceIndex
  optionally skin position/normal/tangent by 2 or 4 bones
  transform to world and clip space
  output uv, world position, TBN basis, screen helpers, instance id

PS:
  read uv/world position/TBN/front-facing/instance id
  build V, N, T, B
  sample base color res54
  convert/apply 32^3 LUT via res49
  sample normal map res56 and reconstruct tangent-space normal
  sample screen auxiliary buffer res38
  sample multi-scale 3D probe/volume textures res40-res45
  evaluate skin ramp res48 and stylized skin color mixing
  loop dynamic lights, evaluate direct diffuse/specular
  sample shadow depth res37 with compare/PCF-like filtering
  combine indirect, direct, skin ramp, specular, masks
  apply fog/atmosphere/volume fallback
  write RT0 HDR color and RT1 packed auxiliary data
```

## 6. 结论

EID 979 是角色皮肤材质的一次完整主光照绘制。其特点是：

- VS 与角色其他部件一致，包含压缩法线/切线解码、骨骼蒙皮、实例矩阵变换和多组 varyings 输出。
- PS 不是简单 deferred GBuffer 写入，而是 forward-like 的材质和光照合成，最后写双 MRT。
- 皮肤颜色由 `res54` base color、`res49` 32^3 LUT atlas、`res48` 1D ramp、屏幕辅助 buffer 和材质参数共同调制。
- 间接光照依赖多张 3D 体积/探针纹理，动态光使用 shadow depth `res37` 做 PCF-like 阴影。
- 最终 RT0 是 HDR lighting/color，RT1 是 packed 后处理或辅助 GBuffer 数据。

完整 SPIR-V 反编译已单独保存为：

- `doc/附录/LuoXi_skin_EID979_vs_spirv.txt`
- `doc/附录/LuoXi_skin_EID979_ps_spirv.txt`
