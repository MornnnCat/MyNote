# LuoXi 武器渲染 Shader 分析：EID 1013

分析对象来自 `D:\ProgramData\RenderXtData\LuoXi.rdc`：

| 项目 | 值 |
|---|---|
| Draw | EID 1013, `vkCmdDrawIndexed(23679, 1)` |
| Topology | `TriangleStrip` |
| Instance count | 1 |
| Vertex shader | `ResourceId::68043`, entry `main` |
| Pixel shader | `ResourceId::68044`, entry `main` |
| Texture count | 19 |
| RT0 | `ResourceId::33297`, 2560x1440, `R11G11B10_FLOAT` |
| RT1 | `ResourceId::33259`, 2560x1440, `R10G10B10A2_UNORM` |
| Depth/stencil | `ResourceId::33254`, 2560x1440, `D32S8` |
| VS 反编译附录 | `doc/附录/LuoXi_weapon_EID1013_vs_spirv.txt` |
| PS 反编译附录 | `doc/附录/LuoXi_weapon_EID1013_ps_spirv.txt` |

该 draw 是角色主渲染 MRT pass 中的武器材质。整体框架与衣服材质接近，但绑定了武器专用的 2048 材质贴图、BC5 法线贴图、BC6 cube map 反射和多张 mask/detail 贴图。

## 1. 固定管线状态

使用 RenderDoc 1.45 Vulkan API-specific state 读取到的状态：

| 状态 | 值 |
|---|---|
| Blend RT0 | enabled = true，color/alpha = `One, Zero, Add`，等效直接覆盖 |
| Blend RT1 | enabled = false |
| Depth | test 开启，write 开启，compare = `Equal` |
| Stencil | test 关闭 |
| Rasterizer | cull = `NoCull`，frontCCW = true，fill = solid |
| Viewport | `0,0,2560,1440`, depth `0..1` |
| Scissor | `0,0,2560,1440` |

虽然 RT0 blend flag 为开启，但 blend factor 是 `src * 1 + dst * 0`，因此实际效果仍是覆盖写入，不是透明混合。

PS 附录 `doc/附录/LuoXi_weapon_EID1013_ps_spirv.txt` 中在约 1977、2002 行存在两处 `Kill()`。因此该 draw 虽然不是靠固定管线 alpha blend 透明混合，但存在 shader 内部片元丢弃/clip-like 分支，应视为材质逻辑中的裁剪行为。

## 2. 顶点 Shader

### 2.1 输入布局

| 输入 | VB | Offset | Format | 推断用途 |
|---|---:|---:|---|---|
| `_input0` | 0 | 0 | `R32G32B32_FLOAT` | position |
| `_input2` | 0 | 12 | `R32G32B32_FLOAT` | normal |
| `_input3` | 0 | 24 | `R32G32B32A32_FLOAT` | tangent + handedness |
| `_input1` | 1 | 0 | `R32G32_FLOAT` | UV0 |
| `_input4` | 1 | 0 | `R32G32_FLOAT` | UV1 或复用 UV |
| `_input5` | 1 | 0 | `R32G32_FLOAT` | UV2 或复用 UV |
| `_input6` | 2 | 0 | `R32G32B32_FLOAT` | auxiliary/deformed position |
| `_input7` | 2 | 12 | `R32G32B32_FLOAT` | auxiliary/deformed normal |
| `_input9` | 3 | 16 | `R8G8B8A8_UNORM` | vertex color / material mask |
| `_input10` | 3 | 0 | `R8G8B8A8_UINT` | packed index / bone index / material id |

武器顶点输入是展开式布局，不像皮肤/部分眼睛 draw 那样从 packed float 中解码 10-bit normal。position、normal、tangent 都直接以 float 向量提供，适合硬表面材质和稳定法线反射。

### 2.2 常量与 buffer

| 资源 | Bind point | 大小 | 推断用途 |
|---|---:|---:|---|
| `uniforms26` | 14 | 4512 | frame/view、相机、环境、雾/大气参数 |
| `uniforms33` | 0 | 336 | 武器材质/对象参数 |
| `uniforms29` | 0 | 65536 | per-instance/object 数据 |
| `ssbo31` | 16 | buffer | 实例/骨骼/附加矩阵数据 |

VS 输出 UV、world position、world normal、tangent4、额外 UV、screen helpers、辅助方向和 flat instance id，供片元阶段构建 TBN、反射方向和 MRT 辅助数据。

## 3. 片元资源绑定

| Slot | 名称 | ResourceId | Bind | 尺寸/格式 | 推断用途 |
|---:|---|---|---:|---|---|
| 0 | `res39` | `ResourceId::33224` | 18 | 2560x1440 `R8G8_UNORM` | 屏幕空间辅助 buffer |
| 1 | `res38` | `ResourceId::7641` | 20 | 6144x4096 `D16` | shadow map |
| 2 | `res63` | `ResourceId::14387` | 22 | 4x4 `R8G8B8A8_SRGB` | 小型 LUT/fallback |
| 3 | `res46` | `ResourceId::13449` | 23 | 128x192 `R8G8B8A8_UNORM` | 体积 probe/方向系数 |
| 4 | `res44` | `ResourceId::13433` | 24 | 128x192 `R8G8B8A8_UNORM` | 体积 probe/方向系数 |
| 5 | `res42` | `ResourceId::13422` | 25 | 128x192 `R8G8B8A8_UNORM` | 体积 probe/方向系数 |
| 6 | `res45` | `ResourceId::13442` | 26 | 128x64 `R11G11B10_FLOAT` | HDR 体积光照权重 |
| 7 | `res43` | `ResourceId::13425` | 27 | 128x64 `R11G11B10_FLOAT` | HDR 体积光照权重 |
| 8 | `res41` | `ResourceId::13419` | 28 | 128x64 `R11G11B10_FLOAT` | HDR 体积光照权重 |
| 9 | `res68` | `ResourceId::65948` | 29 | 1x1 `R8G8B8A8_SRGB` | fallback 3D volume |
| 10 | `res50` | `ResourceId::4771` | 32 | 256x256 `BC7_UNORM` | detail/mask |
| 11 | `res49` | `ResourceId::642` | 35 | 1024x1024 `BC7_UNORM` | triplanar/detail/mask |
| 12 | `res60` | `ResourceId::12357` | 36 | 128x128 `BC6_UFLOAT` cube | environment reflection / IBL |
| 13 | `res55` | `ResourceId::78842` | 5 | 2048x2048 `BC7_UNORM` | material mask / ORM |
| 14 | `res61` | `ResourceId::28501` | 6 | 256x256 `BC7_SRGB` | decal/detail color |
| 15 | `res57` | `ResourceId::77185` | 7 | 2048x2048 `BC5_UNORM` | normal map |
| 16 | `res53` | `ResourceId::77191` | 8 | 2048x2048 `BC7_SRGB` | base color/albedo |
| 17 | `res52` | `ResourceId::4645` | 0 | 512x512 `BC3_SRGB` | triplanar/environment detail |
| 18 | `res51` | `ResourceId::660` | 1 | 1024x1024 `BC7_UNORM` | auxiliary detail/mask |

资源用途均为基于采样位置、格式和尺寸的推断，capture 中只提供 `resXX` 名称。

## 4. 片元 Shader 逻辑

### 4.1 材质贴图与颜色

PS 首先采样 `res53` 2048x2048 `BC7_SRGB`，并乘材质颜色参数：

```text
base = Sample(res53, uv, mipBias).rgb * uniforms48._child24.rgb;
```

随后采样 `res55` 2048x2048 `BC7_UNORM` 作为 mask/材质参数。SPIR-V 中读取 `x/y/z/w` 并组合使用，推断用于 roughness、metallic、AO、区域 mask 或风格化材质控制。

```text
mask = Sample(res55, uv, mipBias);
material = mix(..., mask channels);
```

`res61` 256x256 `BC7_SRGB` 在后段以额外 UV transform 采样，推断为 decal/detail color 或局部发光/装饰贴图。

### 4.2 法线重建

`res57` 是 2048x2048 `BC5_UNORM`，PS 使用双通道重建切线空间法线：

```text
n.xy = sample.wy * 2 - 1;
n.z = sqrt(max(0, 1 - dot(n.xy, n.xy)));
n.xy *= normalStrength;
N = normalize(TBN * n);
```

武器材质的顶点 tangent 是 float4 直接输入，结合 BC5 normal map，说明该 draw 的高光和 cube map 反射主要依赖较高精度的硬表面法线。

### 4.3 Detail / Triplanar 采样

PS 多次以 world/object space 方向分量采样 `res49/res51/res52`：

```text
weights = abs(worldNormal) / dot(abs(worldNormal), 1);
xz = Sample(detailTex, pos.xz);
xy = Sample(detailTex, pos.xy);
zy = Sample(detailTex, pos.zy);
detail = xz * weights.y + xy * weights.z + zy * weights.x;
```

这类模式用于减少武器局部拉伸，给硬表面添加空间连续的细节、污渍、边缘 mask 或环境调色。

### 4.4 Cube map 反射

`res60` 是 128x128 `BC6_UFLOAT` cube texture。PS 中存在：

```text
Image<float, Cube> cube = res60;
reflection = SampleLod(cube, reflectDir, roughnessLod);
```

反编译中 LOD 近似由 roughness-like 参数计算：

```text
lod = roughness * 1.2 + 5.0;
env = SampleLod(res60, reflectionDir, lod).rgb;
```

推断这是武器金属/高光环境反射或 IBL。BC6_UFLOAT 格式说明该 cube map 保存 HDR 环境信息。

### 4.5 体积光照与阴影

与角色其他材质一致，武器 PS 使用多组 3D volume/probe：

```text
coord = frac((worldPos * scale + 0.5) * volumeDim);
weight = SampleLod(volumeWeightTex, coord, 0);
dir0/dir1/dir2 = SampleLod(volumeDirTex, y / 3 + band, 0).xyz * 4 - 2;
```

阴影使用 `res38` / `ResourceId::7641` 的 D16 shadow map，并通过多次 `ImageSampleDrefExplicitLod` 做 PCF-like depth compare。该阴影结果参与动态光和直接光照衰减。

### 4.6 Fog / Atmosphere

PS 尾部保留了与皮肤、眼睛、衣服相同的雾/大气合成路径：根据视距、相机高度、指数衰减和 fallback volume 计算透射率，再将雾色与 lit color 合成到 RT0。

## 5. MRT 输出

PS 输出签名：

| 输出 | 目标 | 推断含义 |
|---|---|---|
| `_output0` | RT0 `R11G11B10_FLOAT` | HDR lighting/color |
| `_output1` | RT1 `R10G10B10A2_UNORM` | packed auxiliary/GBuffer-like 数据 |

该 draw 的 depth compare 为 `Equal`，并写入双 MRT。武器材质在 shader 内完成光照、反射、阴影、雾和材质合成，然后以覆盖方式写入 RT0/RT1。

## 6. 结论

- EID 1013 是武器硬表面材质，使用展开式 normal/tangent 顶点输入、2048 base/mask/normal 贴图和 BC6 HDR cube map。
- 相比皮肤/眼睛，武器更强调 normal map、triplanar/detail 和 cube map reflection。
- 它仍复用角色主 pass 的通用框架：屏幕辅助 buffer、volume probe、shadow map、动态光、fog/atmosphere 和双 MRT。
- 固定管线上 RT0 blend 虽开启但等效覆盖；RT1 不混合；depth 使用 `Equal`，说明该 pass 很可能在已有角色深度基础上执行等深主材质写入。
- 补充核查：EID 1013 PS 中存在两处 `Kill()`，所以“不是透明混合”不等于“没有片元裁剪”。更准确的描述是：固定管线输出为覆盖写入，但 shader 内部仍可按材质/噪声/阈值条件丢弃片元。
