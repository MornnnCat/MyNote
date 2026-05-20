# LuoXi 角色主 MRT Pass 覆盖范围核查

本文用于补充说明现有 shader 分析报告的覆盖范围，避免把已详细分析的 draw 误读为完整角色主 pass。

## 1. Pass 范围

RenderDoc MCP `analyze_render_passes` 读取到角色主 MRT pass 为：

| 项目 | 值 |
|---|---|
| Pass index | 16 |
| Event range | EID 960 - 1095 |
| Draw count | 28 |
| RT0 | `ResourceId::33297`, 2560x1440, `R11G11B10_FLOAT` |
| RT1 | `ResourceId::33259`, 2560x1440, `R10G10B10A2_UNORM` |
| Depth/stencil | `ResourceId::33254`, 2560x1440, `D32S8` |

当前已有详细报告覆盖了 12 个 draw：

```text
960, 965, 970, 979, 984, 989, 999, 1004, 1008, 1013, 1018, 1023
```

这些报告覆盖脸、衣服主材质/飘带、皮肤、眼睛/睫毛、武器、毛发/镂空部件和头发主体，但并不等于覆盖了该 pass 的全部 draw。

## 2. 未详细展开的 draw

以下 16 个 draw 也属于同一角色主 MRT pass，但尚未写成逐 draw shader 分析：

| EID | Index count | VS | PS | Texture count | 备注 |
|---:|---:|---|---|---:|---|
| 974 | 2766 | `ResourceId::40478` | `ResourceId::40479` | 20 | 与衣服主材质同 shader/同资源组，可能是同材质小部件 |
| 994 | 2448 | `ResourceId::47383` | `ResourceId::47384` | 18 | 另一套角色材质变体 |
| 1029 | 5400 | `ResourceId::1906` | `ResourceId::1907` | 1 | 简化材质/贴图较少的部件 |
| 1034 | 600 | `ResourceId::76568` | `ResourceId::76569` | 2 | 小部件或贴花类材质 |
| 1039 | 36 x 2 instances | `ResourceId::67798` | `ResourceId::67799` | 2 | 极低面数实例绘制 |
| 1044 | 6 | `ResourceId::73694` | `ResourceId::73695` | 3 | 极低面数材质/屏幕或局部片 |
| 1050 | 10590 | `ResourceId::40653` | `ResourceId::40654` | 14 | 后续角色材质变体，index count 与脸部 EID 960 相同 |
| 1055 | 14958 | `ResourceId::40658` | `ResourceId::40659` | 12 | 后续衣服/部件变体 |
| 1060 | 59679 | `ResourceId::40658` | `ResourceId::40659` | 12 | 与 EID 1055 同 shader |
| 1064 | 2766 | `ResourceId::40658` | `ResourceId::40659` | 12 | 与 EID 1055 同 shader |
| 1069 | 23220 | `ResourceId::40661` | `ResourceId::40662` | 13 | 后续皮肤/身体类变体，index count 与 EID 979 相同 |
| 1074 | 888 | `ResourceId::40671` | `ResourceId::40672` | 12 | 后续飘带/部件变体 |
| 1079 | 14280 | `ResourceId::40671` | `ResourceId::40672` | 12 | 与 EID 1074 同 shader |
| 1083 | 46845 | `ResourceId::40671` | `ResourceId::40672` | 12 | 与 EID 1074 同 shader |
| 1089 | 41307 | `ResourceId::40690` | `ResourceId::40691` | 15 | 后续头发/主体类变体，index count 与 EID 1023 相同 |
| 1095 | 240 | `ResourceId::73696` | `ResourceId::73697` | 2 | 小型反射/辅助类 draw |

## 3. 固定管线状态说明

当前 RenderDoc MCP 通用 pipeline state 接口在该 Vulkan capture 上仍返回如下 warning：

```text
GetColorBlend / GetDepthState / GetStencilState / GetRasterizer not available
```

因此，现有报告中凡是列出 blend、depth、stencil、rasterizer、viewport 或 scissor 的内容，应理解为来自 RenderDoc 1.45 Vulkan API-specific state 的额外读取结果；仅用 MCP `get_draw_call_state` 时不能完整验证这些固定功能字段。MCP 可稳定验证的是 draw 参数、shader ResourceId、纹理绑定、RT/Depth target 和 pass 归属。

## 4. 已确认修正点

- EID 1013 武器 PS 附录中存在两处 `Kill()`，原报告遗漏了 shader 内片元丢弃/clip-like 行为说明。
- 角色主 MRT pass 实际包含 28 个 draw，现有详细报告只覆盖其中 12 个，需要把未覆盖的 16 个 draw 明确列为尚未详细展开。
