# LuoXi 毛发与镂空衣物 Shader 分析：EID 1018

分析对象来自 `D:\ProgramData\RenderXtData\LuoXi.rdc`：

| 项目 | 值 |
|---|---|
| Draw | EID 1018, `vkCmdDrawIndexed(21849, 1)` |
| 部件 | 毛发以及镂空衣物：兽耳、狼尾、蕾丝 |
| Topology | `TriangleStrip` |
| Instance count | 1 |
| Vertex shader | `ResourceId::40557`, entry `main` |
| Pixel shader | `ResourceId::40558`, entry `main` |
| Texture count | 18 |
| RT0 | `ResourceId::33297`, 2560x1440, `R11G11B10_FLOAT` |
| RT1 | `ResourceId::33259`, 2560x1440, `R10G10B10A2_UNORM` |
| Depth/stencil | `ResourceId::33254`, 2560x1440, `D32S8` |
| VS 反编译附录 | `doc/附录/LuoXi_hair_cutout_EID1018_vs_spirv.txt` |
| PS 反编译附录 | `doc/附录/LuoXi_hair_cutout_EID1018_ps_spirv.txt` |

该 draw 属于角色主渲染 MRT pass，材质覆盖毛发、兽耳、狼尾和蕾丝一类双面/镂空部件。它绑定了 2048x2048 base/mask/normal 贴图、1024x32 LUT、256x1 ramp、屏幕辅助 buffer 和体积光照纹理。

## 1. 固定管线状态

使用 RenderDoc 1.45 Vulkan API-specific state 读取到的状态：

| 状态 | 值 |
|---|---|
| Blend RT0/RT1 | 均 disabled，write mask = 15 |
| Depth | test 开启，write 开启，compare = `Equal` |
| Stencil | test 关闭 |
| Rasterizer | cull = `NoCull`，frontCCW = true，fill = solid |
| Viewport | `0,0,2560,1440`, depth `0..1` |
| Scissor | `0,0,2560,1440` |

关键结论：该 draw 没有固定管线 alpha blend，也未在 SPIR-V 中找到 `Discard` / `Kill` / `Demote` / `Clip`。因此“镂空”更可能由贴图 mask 调制输出、几何本身、等深主 pass 或后处理分类实现，而不是传统 alpha blend 或显式 alpha test。

`NoCull` 对毛发、兽耳和蕾丝很重要：双面薄片在正反面都需要参与着色，避免背面被剔除造成镂空面片消失。

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

该 VS 与角色皮肤/衣服类 draw 接近：使用 packed normal/tangent 数据、骨骼权重和骨骼索引。顶点阶段会进行压缩法线/切线解码、蒙皮/实例矩阵变换，并输出 UV、world position、normal、tangent4、screen helpers、辅助方向和 flat instance id。

### 2.2 常量与 buffer

| 资源 | Bind point | 大小 | 推断用途 |
|---|---:|---:|---|
| `uniforms24` | 14 | 4512 | frame/view、相机、环境、雾/大气参数 |
| `uniforms31` | 0 | 400 | 毛发/镂空材质参数 |
| `uniforms27` | 0 | 65536 | per-instance/object 数据 |
| `ssbo29` | 16 | buffer | 骨骼/形变/实例矩阵数据 |

`uniforms31` 比普通衣服材质参数块更大，推断包含额外的 mask、边缘、透空或毛发调制参数。

## 3. 片元资源绑定

| Slot | 名称 | ResourceId | Bind | 尺寸/格式 | 推断用途 |
|---:|---|---|---:|---|---|
| 0 | `res39` | `ResourceId::33224` | 18 | 2560x1440 `R8G8_UNORM` | 屏幕空间辅助 buffer |
| 1 | `res38` | `ResourceId::7641` | 20 | 6144x4096 `D16` | shadow map |
| 2 | `res60` | `ResourceId::14387` | 22 | 4x4 `R8G8B8A8_SRGB` | 小型 LUT/fallback |
| 3 | `res46` | `ResourceId::13449` | 23 | 128x192 `R8G8B8A8_UNORM` | 体积 probe/方向系数 |
| 4 | `res44` | `ResourceId::13433` | 24 | 128x192 `R8G8B8A8_UNORM` | 体积 probe/方向系数 |
| 5 | `res42` | `ResourceId::13422` | 25 | 128x192 `R8G8B8A8_UNORM` | 体积 probe/方向系数 |
| 6 | `res45` | `ResourceId::13442` | 26 | 128x64 `R11G11B10_FLOAT` | HDR 体积光照权重 |
| 7 | `res43` | `ResourceId::13425` | 27 | 128x64 `R11G11B10_FLOAT` | HDR 体积光照权重 |
| 8 | `res41` | `ResourceId::13419` | 28 | 128x64 `R11G11B10_FLOAT` | HDR 体积光照权重 |
| 9 | `res65` | `ResourceId::65948` | 29 | 1x1 `R8G8B8A8_SRGB` | fallback 3D volume |
| 10 | `res27` | `ResourceId::33230` | 37 | 2560x1440 `R32_FLOAT` | 屏幕深度/线性深度/透明辅助 |
| 11 | `res50` | `ResourceId::31040` | 5 | 256x256 `R8G8B8A8_UNORM` | detail/ramp 辅助或毛发色调表 |
| 12 | `res51` | `ResourceId::68181` | 6 | 1024x32 `BC7_SRGB` | 32 层 LUT atlas |
| 13 | `res56` | `ResourceId::47847` | 7 | 2048x2048 `BC7_UNORM` | material mask / alpha / ORM |
| 14 | `res49` | `ResourceId::29897` | 8 | 256x1 `R8G8B8A8_UNORM` | 1D ramp |
| 15 | `res58` | `ResourceId::47783` | 9 | 2048x2048 `BC5_UNORM` | normal map |
| 16 | `res54` | `ResourceId::47355` | 10 | 2048x2048 `BC7_SRGB` | base color/albedo |
| 17 | `res53` | `ResourceId::660` | 0 | 1024x1024 `BC7_UNORM` | auxiliary detail/mask |

资源用途是基于格式、尺寸、采样方式和材质类型的推断；capture 中没有材质参数原始名。

## 4. 片元 Shader 逻辑

### 4.1 Base Color、Mask 与 LUT

PS 首先采样 `res54` 2048x2048 `BC7_SRGB`，并乘材质颜色参数：

```text
base = Sample(res54, uv, mipBias).rgb * uniforms48._child24.rgb;
alphaOrBaseA = Sample(res54, uv).a;
```

随后采样 `res56` 2048x2048 `BC7_UNORM` 作为 mask/材质参数。其通道参与颜色混合、边缘控制、明暗或镂空区域调制。因为 shader 没有 discard，mask 更可能用于输出调制、材质分类或深度相关软化，而不是直接丢弃像素。

`res51` 是 1024x32 `BC7_SRGB`，SPIR-V 中呈现典型 32 层 LUT atlas 采样：

```text
zSlice = color.b * 31;
sample0 = SampleLod(res51, atlasUV(slice0), 0);
sample1 = SampleLod(res51, atlasUV(slice0 + 1), 0);
color = mix(sample0.rgb, sample1.rgb, frac(zSlice));
```

这说明毛发/蕾丝颜色也经过角色统一的风格化色彩校正。

### 4.2 BC5 法线重建

`res58` 是 2048x2048 `BC5_UNORM`，PS 使用双通道重建切线空间法线：

```text
n.xy = sample.wy * 2 - 1;
n.z = sqrt(max(0, 1 - dot(n.xy, n.xy)));
n.xy *= normalStrength;
N = normalize(TBN * n);
```

毛发、兽耳和蕾丝都需要稳定的细节法线来维持细小面片上的高光方向。配合 `NoCull`，shader 还根据 `FrontFacing` 对法线方向做正反面修正。

### 4.3 Ramp 与毛发/蕾丝调色

`res49` 256x1 `R8G8B8A8_UNORM` 以 `dotLike * 0.5 + 0.5` 形式采样：

```text
u = saturate(dotLike * 0.5 + 0.5);
ramp = SampleLod(res49, float2(u, 0.5), 0);
```

其 RGB/A 参与明暗边界、边缘透光或风格化阴影控制。毛发和蕾丝通常需要较硬的明暗分界与边缘控制，这个 ramp 是主要调制来源之一。

`res50` 256x256 `R8G8B8A8_UNORM` 也被按派生坐标采样，推断是毛发色调表、边缘调制表或局部透空/遮挡辅助贴图。

### 4.4 屏幕空间深度/辅助采样

该 draw 除 `res39` 屏幕辅助 buffer 外，还绑定了 `res27` 2560x1440 `R32_FLOAT`。PS 中对 `res27` 做 screen-space 采样：

```text
screenDepthLike = SampleLod(res27, screenUV, 0).x;
```

结合毛发和镂空材质类型，推断它用于以下之一：

- 与已有深度比较，处理薄片边缘、穿插或软遮挡。
- 为毛发/蕾丝做屏幕空间 fade 或深度相关透明感。
- 提供后处理/分类所需的辅助深度。

由于固定管线 blend 关闭且没有 discard，这类 screen-space 辅助很可能是实现“镂空感”或边缘稳定性的关键。

### 4.5 体积光照与阴影

与其他角色材质一致，PS 使用多组 3D volume/probe 纹理：

```text
coord = frac((worldPos * scale + 0.5) * volumeDim);
weight = SampleLod(volumeWeightTex, coord, 0);
dir0/dir1/dir2 = SampleLod(volumeDirTex, y / 3 + band, 0).xyz * 4 - 2;
```

阴影使用 `res38` / `ResourceId::7641` 的 D16 shadow map，多次 `ImageSampleDrefExplicitLod` 做 PCF-like depth compare，并参与动态光衰减。

### 4.6 Fog / Atmosphere

PS 尾部保留角色主 pass 的雾/大气路径：根据视距、相机高度、指数衰减和 fallback volume 计算透射率，再与 lit color 合成到 RT0。

## 5. MRT 输出

PS 输出签名：

| 输出 | 目标 | 推断含义 |
|---|---|---|
| `_output0` | RT0 `R11G11B10_FLOAT` | HDR lighting/color |
| `_output1` | RT1 `R10G10B10A2_UNORM` | packed auxiliary/GBuffer-like 数据 |

Depth compare 为 `Equal`，并且 depth write 开启。该 draw 应在已有角色深度基础上进行等深主材质写入。毛发/蕾丝的 mask 不通过固定管线混合，而是参与 shader 内部颜色、辅助输出和屏幕空间控制。

## 6. 结论

- EID 1018 是双面毛发/镂空衣物材质，使用压缩/蒙皮式角色顶点路径和 `NoCull`。
- 它绑定 2048 base/mask/normal 贴图、1024x32 LUT、256x1 ramp，以及额外 `R32_FLOAT` 屏幕辅助深度。
- 没有固定管线 alpha blend，也未发现显式 discard/kill；镂空效果更可能由 mask 调制、几何、等深 pass、屏幕深度辅助和后处理分类共同实现。
- 该材质仍复用角色主 pass 的通用 lighting 框架：screen buffer、volume probe、shadow map、动态光、fog/atmosphere 和双 MRT 输出。
