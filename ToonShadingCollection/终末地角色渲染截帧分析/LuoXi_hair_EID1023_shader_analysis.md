# LuoXi 头发 Shader 分析：EID 1023

分析对象来自 `D:\ProgramData\RenderXtData\LuoXi.rdc`：

| 项目 | 值 |
|---|---|
| Draw | EID 1023, `vkCmdDrawIndexed(41307, 1)` |
| 部件 | 头发 |
| Topology | `TriangleStrip` |
| Instance count | 1 |
| Vertex shader | `ResourceId::40560`, entry `main` |
| Pixel shader | `ResourceId::40561`, entry `main` |
| Texture count | 19 |
| RT0 | `ResourceId::33297`, 2560x1440, `R11G11B10_FLOAT` |
| RT1 | `ResourceId::33259`, 2560x1440, `R10G10B10A2_UNORM` |
| Depth/stencil | `ResourceId::33254`, 2560x1440, `D32S8` |
| VS 反编译附录 | `doc/附录/LuoXi_hair_EID1023_vs_spirv.txt` |
| PS 反编译附录 | `doc/附录/LuoXi_hair_EID1023_ps_spirv.txt` |

该 draw 是头发主体材质。它与 EID 1018 毛发/镂空衣物共享屏幕辅助、R32 深度辅助、LUT/ramp、体积光照和 shadow map 框架，但绑定了更多头发专用 2048 贴图，并且固定管线使用 back-face culling。

## 1. 固定管线状态

使用 RenderDoc 1.45 Vulkan API-specific state 读取到的状态：

| 状态 | 值 |
|---|---|
| Blend RT0/RT1 | 均 disabled，write mask = 15 |
| Depth | test 开启，write 开启，compare = `Equal` |
| Stencil | test 关闭 |
| Rasterizer | cull = `Back`，frontCCW = true，fill = solid |
| Viewport | `0,0,2560,1440`, depth `0..1` |
| Scissor | `0,0,2560,1440` |

SPIR-V 中未发现 `Discard` / `Kill`。因此头发并不是靠固定管线 alpha blend 或显式 discard 绘制；alpha/mask 更可能在 shader 内参与颜色、边缘、RT1 分类或屏幕空间深度调制。

与 EID 1018 的 `NoCull` 不同，本 draw 使用 `Back` culling，说明头发主体网格更接近有厚度/定向的发片或发束集合，背面不需要全部保留。

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

VS 使用角色通用的压缩/蒙皮式输入：packed normal/tangent、skin weights、bone indices、alternate/deformed position。顶点阶段负责解码法线/切线、蒙皮/实例变换，并输出 UV、world position、normal、tangent4、screen helpers、辅助方向和 flat instance id。

### 2.2 常量与 buffer

| 资源 | Bind point | 大小 | 推断用途 |
|---|---:|---:|---|
| `uniforms24` | 14 | 4512 | frame/view、相机、环境、雾/大气参数 |
| `uniforms31` | 0 | 448 | 头发材质参数 |
| `uniforms27` | 0 | 65536 | per-instance/object 数据 |
| `ssbo29` | 16 | buffer | 骨骼/形变/实例矩阵数据 |

`uniforms31` 为 448 bytes，比 EID 1018 的 400 bytes 更大，推断包含更多头发专用的颜色、分层、边缘、深度或高光控制参数。

## 3. 片元资源绑定

| Slot | 名称 | ResourceId | Bind | 尺寸/格式 | 推断用途 |
|---:|---|---|---:|---|---|
| 0 | `res39` | `ResourceId::33224` | 18 | 2560x1440 `R8G8_UNORM` | 屏幕空间辅助 buffer |
| 1 | `res38` | `ResourceId::7641` | 20 | 6144x4096 `D16` | shadow map |
| 2 | `res61` | `ResourceId::14387` | 22 | 4x4 `R8G8B8A8_SRGB` | 小型 LUT/fallback |
| 3 | `res46` | `ResourceId::13449` | 23 | 128x192 `R8G8B8A8_UNORM` | 体积 probe/方向系数 |
| 4 | `res44` | `ResourceId::13433` | 24 | 128x192 `R8G8B8A8_UNORM` | 体积 probe/方向系数 |
| 5 | `res42` | `ResourceId::13422` | 25 | 128x192 `R8G8B8A8_UNORM` | 体积 probe/方向系数 |
| 6 | `res45` | `ResourceId::13442` | 26 | 128x64 `R11G11B10_FLOAT` | HDR 体积光照权重 |
| 7 | `res43` | `ResourceId::13425` | 27 | 128x64 `R11G11B10_FLOAT` | HDR 体积光照权重 |
| 8 | `res41` | `ResourceId::13419` | 28 | 128x64 `R11G11B10_FLOAT` | HDR 体积光照权重 |
| 9 | `res66` | `ResourceId::65948` | 29 | 1x1 `R8G8B8A8_SRGB` | fallback 3D volume |
| 10 | `res27` | `ResourceId::33230` | 37 | 2560x1440 `R32_FLOAT` | 屏幕深度/线性深度/头发辅助 |
| 11 | `res59` | `ResourceId::74817` | 5 | 2048x2048 `BC7_UNORM` | 头发 mask/alpha/材质参数 |
| 12 | `res51` | `ResourceId::31072` | 6 | 256x256 `R8G8B8A8_UNORM` | 头发色调/边缘辅助表 |
| 13 | `res52` | `ResourceId::68181` | 7 | 1024x32 `BC7_SRGB` | 32 层 LUT atlas |
| 14 | `res57` | `ResourceId::74837` | 8 | 2048x2048 `BC7_UNORM` | 第二组头发 mask/detail |
| 15 | `res49` | `ResourceId::28518` | 9 | 512x512 `BC7_UNORM` | 头发 flow/strand/detail mask |
| 16 | `res50` | `ResourceId::5098` | 10 | 256x1 `R8G8B8A8_UNORM` | 1D ramp |
| 17 | `res55` | `ResourceId::74844` | 11 | 2048x2048 `BC7_SRGB` | base color/albedo |
| 18 | `res54` | `ResourceId::660` | 0 | 1024x1024 `BC7_UNORM` | auxiliary detail/mask |

资源用途为基于格式、尺寸、采样方式和材质类型的推断；capture 中未包含材质参数原始名。

## 4. 片元 Shader 逻辑

### 4.1 Base Color 与多 Mask

PS 首先采样 `res55` 2048x2048 `BC7_SRGB`，并乘材质颜色参数：

```text
base = Sample(res55, uv, mipBias).rgb * uniforms48._child24.rgb;
alphaOrBaseA = Sample(res55, uv).a;
```

随后采样多张头发专用 mask/detail：

- `res59` 2048x2048 `BC7_UNORM`
- `res57` 2048x2048 `BC7_UNORM`
- `res49` 512x512 `BC7_UNORM`
- `res54` 1024x1024 `BC7_UNORM`

这些贴图通道参与边缘、发束、明暗、材质分区和深度相关控制。由于没有 blend/discard，alpha-like 信息更可能进入 shader 内部混合与 RT1 辅助数据，而不是直接控制固定管线透明。

### 4.2 法线与发束方向

PS 使用采样贴图中的双通道重建法线：

```text
n.xy = sample.xy * 2 - 1;
n.z = sqrt(max(0, 1 - dot(n.xy, n.xy)));
n.xy *= normalStrength;
N = normalize(TBN * n);
```

头发材质还额外采样 `res49` 这类 512x512 mask/detail，推断用于发丝 flow、strand mask 或高光方向调制。与普通硬表面不同，头发高光通常沿发丝方向拉伸，材质参数块更大也支持这一判断。

### 4.3 LUT 与 Ramp

`res52` 是 1024x32 `BC7_SRGB`，以 32 层 LUT atlas 方式采样：

```text
zSlice = color.b * 31;
sample0 = SampleLod(res52, atlasUV(slice0), 0);
sample1 = SampleLod(res52, atlasUV(slice0 + 1), 0);
color = mix(sample0.rgb, sample1.rgb, frac(zSlice));
```

`res50` 是 256x1 ramp，用于角度相关明暗、头发边缘或高光色调：

```text
u = saturate(dotLike * 0.5 + 0.5);
ramp = SampleLod(res50, float2(u, 0.5), 0);
```

### 4.4 屏幕空间深度/辅助采样

头发 PS 同时读取：

- `res39` 2560x1440 `R8G8_UNORM`
- `res27` 2560x1440 `R32_FLOAT`

`res39` 通过 `ImageFetch` 按 `FragCoord.xy` 读取，`res27` 通过 screen-space UV 采样。推断它们用于头发与角色/背景的深度关系、边缘软化、遮挡或后处理分类。

```text
screenAux = ImageFetch(res39, int2(FragCoord.xy), 0);
screenDepthLike = SampleLod(res27, screenUV, 0).x;
```

这和 EID 1018 的毛发/镂空衣物很接近，但 EID 1023 使用 Back culling，说明主体头发更多依赖有方向的网格和深度辅助，而非完全双面薄片。

### 4.5 体积光照、阴影与雾

头发 PS 复用角色主 pass 的体积光照：

```text
coord = frac((worldPos * scale + 0.5) * volumeDim);
weight = SampleLod(volumeWeightTex, coord, 0);
dir0/dir1/dir2 = SampleLod(volumeDirTex, y / 3 + band, 0).xyz * 4 - 2;
```

阴影使用 `res38` / `ResourceId::7641` 的 D16 shadow map，多次 `ImageSampleDrefExplicitLod` 做 PCF-like depth compare。尾部继续执行 fog/atmosphere 合成，将雾色和透射率合入 RT0。

## 5. MRT 输出

PS 输出签名：

| 输出 | 目标 | 推断含义 |
|---|---|---|
| `_output0` | RT0 `R11G11B10_FLOAT` | HDR lighting/color |
| `_output1` | RT1 `R10G10B10A2_UNORM` | packed auxiliary/GBuffer-like 数据 |

Depth compare 为 `Equal`，blend 关闭。头发材质在 shader 内完成颜色、mask、深度辅助、光照和雾合成，再覆盖写入双 MRT。

## 6. 结论

- EID 1023 是头发主体材质，使用压缩/蒙皮式顶点路径和 Back culling。
- 该 draw 绑定了多张头发专用 2048 mask/detail/base 贴图，以及 512 flow/detail mask、1024x32 LUT、256x1 ramp 和 `R32_FLOAT` 屏幕深度辅助。
- 没有固定管线 alpha blend，也未发现显式 discard/kill；透明感、发丝边缘和遮挡更可能由 mask、屏幕深度辅助、RT1 分类和后处理共同实现。
- 它与 EID 1018 毛发/镂空衣物共享大部分光照框架，但 EID 1023 更偏头发主体，参数块更大且使用 Back culling。
