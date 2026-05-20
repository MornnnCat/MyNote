# LuoXi 眼睛渲染 Shader 分析

分析对象来自 `D:\ProgramData\RenderXtData\LuoXi.rdc`：

| EID | 部件 | Draw | Index count | VS | PS | Texture count |
|---:|---|---|---:|---|---|---:|
| 984 | 眼球圆片以及眼睛高光 | `vkCmdDrawIndexed()` | 840 | `ResourceId::40489` | `ResourceId::40490` | 14 |
| 989 | 睫毛 | `vkCmdDrawIndexed()` | 1356 | `ResourceId::40497` | `ResourceId::40498` | 14 |

两次 draw 都属于角色主渲染 MRT pass：

| 项目 | 值 |
|---|---|
| Topology | `TriangleStrip` |
| Instance count | 1 |
| RT0 | `ResourceId::33297`, 2560x1440, `R11G11B10_FLOAT` |
| RT1 | `ResourceId::33259`, 2560x1440, `R10G10B10A2_UNORM` |
| Depth/stencil | `ResourceId::33254`, 2560x1440, `D32S8` |
| Shadow depth | `ResourceId::7641`, 6144x4096, `D16` |

使用 RenderDoc 1.45 Vulkan API-specific state 读取到的固定管线状态一致：

| 状态 | EID 984 / EID 989 |
|---|---|
| Blend | 2 个 color attachment，均关闭，write mask = 15 |
| Depth | test 开启，write 开启，compare = `Equal` |
| Stencil | test 关闭 |
| Rasterizer | back-face culling，frontCCW = true，fill = solid |
| Viewport | `0,0,2560,1440`, depth `0..1` |
| Scissor | `0,0,2560,1440` |

完整 SPIR-V 反编译位于：

- `doc/附录/LuoXi_eye_EID984_vs_spirv.txt`
- `doc/附录/LuoXi_eye_EID984_ps_spirv.txt`
- `doc/附录/LuoXi_eye_EID989_vs_spirv.txt`
- `doc/附录/LuoXi_eye_EID989_ps_spirv.txt`

## 1. Shader 分组

EID 984 和 EID 989 使用不同 shader：

| EID | VS 行数 | PS 行数 | 主要差异 |
|---:|---:|---:|---|
| 984 | 742 | 2807 | 眼球/高光材质，顶点输入仍接近压缩蒙皮布局，包含切线输出 |
| 989 | 732 | 2698 | 睫毛材质，顶点输入展开为 float3/float4，PS 输入少了 `_input3` tangent4 |

两者片元 shader 都保留了角色主 pass 的通用框架：屏幕辅助 buffer、shadow map、体积 probe、ramp/LUT、动态光循环、雾/大气合成、双 MRT 输出。

## 2. 顶点 Shader

### 2.1 EID 984 眼球/高光输入

| 输入 | VB | Offset | Format | 推断用途 |
|---|---:|---:|---|---|
| `_input0` | 0 | 0 | `R32G32B32_FLOAT` | position |
| `_input2` | 0 | 12 | `R32_FLOAT` | packed normal / tangent helper |
| `_input3` | 4 | 12 | `R8G8B8A8_UNORM` | vertex color 或 material mask |
| `_input1` | 1 | 0 | `R32G32_FLOAT` | UV |
| `_input4` | 1 | 0 | `R32G32_FLOAT` | 第二组 UV 或 packed tangent 数据 |
| `_input5` | 3 | 0 | `R32G32B32_FLOAT` | alternate/deformed position |
| `_input6` | 3 | 12 | `R32_FLOAT` | alternate packed normal |
| `_input8` | 2 | 0 | `R16G16B16A16_UNORM` | skin weights |
| `_input9` | 2 | 8 | `R8G8B8A8_UINT` | bone indices |

EID 984 的 VS 与皮肤/脸部 draw 的结构接近：从 packed float 中拆 10-bit 分量，执行 octahedral normal decode，再基于 instance data 和 `ssbo29` 做骨骼/实例变换。输出包括 UV、world position、normal、tangent4、screen helpers、辅助方向和 flat instance id。

### 2.2 EID 989 睫毛输入

| 输入 | VB | Offset | Format | 推断用途 |
|---|---:|---:|---|---|
| `_input0` | 0 | 0 | `R32G32B32_FLOAT` | position |
| `_input2` | 0 | 12 | `R32G32B32_FLOAT` | normal |
| `_input3` | 0 | 24 | `R32G32B32A32_FLOAT` | tangent 或 packed material data |
| `_input1` | 1 | 0 | `R32G32_FLOAT` | UV |
| `_input4` | 3 | 0 | `R32G32B32_FLOAT` | auxiliary position/direction |
| `_input5` | 4 | 0 | `R32G32B32_FLOAT` | alternate/deformed position |
| `_input6` | 4 | 12 | `R32G32B32_FLOAT` | alternate/deformed normal |
| `_input8` | 2 | 0 | `R32G32B32A32_FLOAT` | skin weights |
| `_input9` | 2 | 16 | `R32G32B32A32_UINT` | bone indices |

睫毛网格的输入明显更展开：position/normal/tangent/weights/indices 都是 float 或 uint 向量，不再像 EID 984 那样大量使用 10-bit packed normal。推断这是因为睫毛几何更细，透明边缘和朝向稳定性比压缩率更重要。

## 3. 片元资源绑定

### 3.1 EID 984：眼球圆片以及眼睛高光

| Slot | 名称 | ResourceId | Bind | 尺寸/格式 | 推断用途 |
|---:|---|---|---:|---|---|
| 0 | `res37` | `ResourceId::33224` | 18 | 2560x1440 `R8G8_UNORM` | 屏幕空间辅助 buffer |
| 1 | `res36` | `ResourceId::7641` | 20 | 6144x4096 `D16` | shadow map |
| 2 | `res53` | `ResourceId::14387` | 22 | 4x4 `R8G8B8A8_SRGB` | 小型 LUT/fallback |
| 3 | `res44` | `ResourceId::13449` | 23 | 128x192 `R8G8B8A8_UNORM` | 体积 probe/方向系数 |
| 4 | `res42` | `ResourceId::13433` | 24 | 128x192 `R8G8B8A8_UNORM` | 体积 probe/方向系数 |
| 5 | `res40` | `ResourceId::13422` | 25 | 128x192 `R8G8B8A8_UNORM` | 体积 probe/方向系数 |
| 6 | `res43` | `ResourceId::13442` | 26 | 128x64 `R11G11B10_FLOAT` | HDR 体积光照权重 |
| 7 | `res41` | `ResourceId::13425` | 27 | 128x64 `R11G11B10_FLOAT` | HDR 体积光照权重 |
| 8 | `res39` | `ResourceId::13419` | 28 | 128x64 `R11G11B10_FLOAT` | HDR 体积光照权重 |
| 9 | `res58` | `ResourceId::65948` | 29 | 1x1 `R8G8B8A8_SRGB` | fallback volume |
| 10 | `res51` | `ResourceId::49567` | 3 | 256x256 `BC7_SRGB` | 眼睛高光/环境反射 mask 或细节 |
| 11 | `res47` | `ResourceId::57522` | 4 | 256x1 `R8G8B8A8_UNORM` | ramp |
| 12 | `res49` | `ResourceId::49553` | 5 | 512x512 `BC7_SRGB` | 眼球/虹膜/高光 base 或 overlay |
| 13 | `res48` | `ResourceId::660` | 0 | 1024x1024 `BC7_UNORM` | 辅助 mask/detail |

### 3.2 EID 989：睫毛

| Slot | 名称 | ResourceId | Bind | 尺寸/格式 | 推断用途 |
|---:|---|---|---:|---|---|
| 0 | `res36` | `ResourceId::33224` | 18 | 2560x1440 `R8G8_UNORM` | 屏幕空间辅助 buffer |
| 1 | `res35` | `ResourceId::7641` | 20 | 6144x4096 `D16` | shadow map |
| 2 | `res52` | `ResourceId::14387` | 22 | 4x4 `R8G8B8A8_SRGB` | 小型 LUT/fallback |
| 3 | `res43` | `ResourceId::13449` | 23 | 128x192 `R8G8B8A8_UNORM` | 体积 probe/方向系数 |
| 4 | `res41` | `ResourceId::13433` | 24 | 128x192 `R8G8B8A8_UNORM` | 体积 probe/方向系数 |
| 5 | `res39` | `ResourceId::13422` | 25 | 128x192 `R8G8B8A8_UNORM` | 体积 probe/方向系数 |
| 6 | `res42` | `ResourceId::13442` | 26 | 128x64 `R11G11B10_FLOAT` | HDR 体积光照权重 |
| 7 | `res40` | `ResourceId::13425` | 27 | 128x64 `R11G11B10_FLOAT` | HDR 体积光照权重 |
| 8 | `res38` | `ResourceId::13419` | 28 | 128x64 `R11G11B10_FLOAT` | HDR 体积光照权重 |
| 9 | `res57` | `ResourceId::65948` | 29 | 1x1 `R8G8B8A8_SRGB` | fallback volume |
| 10 | `res47` | `ResourceId::29281` | 3 | 1024x32 `BC7_SRGB` | 32 层 LUT atlas |
| 11 | `res46` | `ResourceId::57522` | 4 | 256x1 `R8G8B8A8_UNORM` | ramp |
| 12 | `res50` | `ResourceId::74848` | 5 | 1024x1024 `BC7_SRGB` | 睫毛颜色/alpha |
| 13 | `res49` | `ResourceId::660` | 0 | 1024x1024 `BC7_UNORM` | 睫毛辅助 mask/detail |

资源用途为基于格式、尺寸、采样方式和 SPIR-V 访问位置的推断；capture 中没有材质参数原始名。

## 4. 片元 Shader 逻辑

### 4.1 共同框架

两个 PS 都有以下通用流程：

```text
read uv/world position/normal/front-facing/instance id
sample base color or material texture
apply material color multiplier
sample ramp / LUT
sample screen-space auxiliary buffer
sample multi-scale 3D volume/probe textures
loop lights and evaluate shadow compare from D16 shadow map
combine direct light, indirect/probe light, ramp, masks
apply fog/atmosphere fallback volume
write RT0 HDR color and RT1 packed auxiliary data
```

这种结构与皮肤、脸部、衣服主 pass 一致，区别主要来自材质贴图、顶点输入和局部高光/透明 mask 处理。

### 4.2 EID 984：眼球圆片与眼睛高光

EID 984 片元 shader 先采样 `res49` 一类的眼睛 base/detail 贴图，并乘 `uniforms46._child24.rgb` 这样的材质颜色参数。随后采样 `res47` 256x1 ramp，以视线、法线或光照角度映射出风格化明暗。

反编译中可见典型 ramp 采样：

```text
u = dotLike * 0.5 + 0.5;
ramp = SampleLod(res47, float2(u, 0.5), 0);
```

眼球/高光的特殊点在于 `res51` 256x256 `BC7_SRGB` 会按由法线或视线变换得到的 `[-1,1] -> [0,1]` 坐标采样：

```text
highlightUV = viewOrNormal.xy * 0.5 + 0.5;
highlight = Sample(res51, highlightUV);
```

推断它用于眼睛高光、环境反射或湿润高光 mask。由于固定管线 blend 关闭，高光并不是通过独立透明 blend 叠加，而是在 shader 内合成到 RT0。

### 4.3 EID 989：睫毛

EID 989 的 base 采样来自 `res50` 1024x1024 `BC7_SRGB`，并使用 `res49` 1024x1024 `BC7_UNORM` 作为辅助 mask/detail。睫毛一般需要 alpha 或覆盖率控制，但当前固定管线 blend 关闭，因此透明/边缘处理应在 shader 内通过以下方式之一实现：

- 根据贴图 alpha/mask 调制输出颜色与 RT1 辅助值。
- 使用 depth-equal 主 pass 中已有深度，依赖预处理或 alpha test/clip-like 逻辑。
- 将睫毛作为实体细网格绘制，贴图 mask 仅调色或软化边缘。

PS 中仍有 32 层 LUT atlas 采样模式：

```text
zSlice = color.b * 31;
sample0 = SampleLod(res47, atlasUV(slice0), 0);
sample1 = SampleLod(res47, atlasUV(slice0 + 1), 0);
color = mix(sample0.rgb, sample1.rgb, frac(zSlice));
```

这说明睫毛也经过与其他角色材质相同的风格化色彩校正，而不是简单采样贴图后直接输出。

### 4.4 体积光照与阴影

两个 PS 都采样 `res39/res40/res41/res42/res43/res44` 这类 3D 纹理。SPIR-V 模式与皮肤材质一致：世界坐标缩放后取 fractional cell，再在 3D 纹理 Y 方向分三段采样方向或 SH-like 系数：

```text
coord = frac((worldPos * scale + 0.5) * volumeDim);
weight = SampleLod(volumeWeightTex, coord, 0);
dir0 = SampleLod(volumeDirTex, float3(coord.x, y/3 + 0/3, coord.z), 0).xyz * 4 - 2;
dir1 = SampleLod(volumeDirTex, float3(coord.x, y/3 + 1/3, coord.z), 0).xyz * 4 - 2;
dir2 = SampleLod(volumeDirTex, float3(coord.x, y/3 + 2/3, coord.z), 0).xyz * 4 - 2;
```

阴影使用 `ResourceId::7641` 的 `D16` shadow map，并通过多次 `ImageSampleDrefExplicitLod` 做 PCF-like compare。该阴影逻辑在 EID 984 和 EID 989 中都存在。

## 5. MRT 输出

两个 PS 输出签名相同：

| 输出 | 目标 | 推断含义 |
|---|---|---|
| `_output0` | RT0 `R11G11B10_FLOAT` | HDR lighting/color |
| `_output1` | RT1 `R10G10B10A2_UNORM` | packed auxiliary/GBuffer-like 数据 |

由于 depth compare 为 `Equal` 且 depth write 开启，这两个 draw 应处于角色主深度已经建立后的等深绘制阶段。shader 负责把眼睛/睫毛颜色合入主 MRT，而不是靠固定管线 blend 叠加。

## 6. 结论

- EID 984 是眼球圆片和眼睛高光材质，保留压缩/蒙皮式顶点路径，片元阶段通过眼睛贴图、ramp、局部高光贴图、体积光照和阴影在 shader 内完成合成。
- EID 989 是睫毛材质，顶点输入更展开，贴图重点是 1024x1024 睫毛颜色/alpha 与辅助 mask；固定管线 blend 关闭，透明或边缘控制应在 shader 内处理。
- 两者都复用角色主 pass 的通用 lighting 框架：screen buffer、volume probe、shadow map、LUT/ramp、fog/atmosphere、双 MRT 输出。
- 附录文件已保存完整 VS/PS SPIR-V，便于后续逐行追踪。
