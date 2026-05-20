# RenderDoc MCP 固定管线状态读取问题说明

## 问题现象

在分析 Vulkan capture `D:\ProgramData\RenderXtData\LuoXi.rdc` 的 EID 979 时，`renderdoc-mcp` 读取固定管线状态出现 warning：

```text
'renderdoc.PipeState' object has no attribute 'GetColorBlend'
'renderdoc.PipeState' object has no attribute 'GetDepthState'
'renderdoc.PipeState' object has no attribute 'GetStencilState'
'renderdoc.PipeState' object has no attribute 'GetRasterizer'
'renderdoc.PipeState' object has no attribute 'GetViewports'
'renderdoc.PipeState' object has no attribute 'GetScissors'
```

受影响的是 MCP 输出中的固定管线字段，例如 blend、depth、stencil、rasterizer、viewport/scissor。Shader 反编译、资源绑定、RT/Depth target、vertex input 等不受影响。

## 当前版本

| 组件 | 版本 |
|---|---|
| `renderdoc-mcp` | `0.2.0` |
| `renderdoc-mcp` commit | `4911c47` |
| RenderDoc Python API | `1.45` |
| `renderdoc.dll` | `v1.45`, `1.45.0.0` |
| RenderDoc source | `v1.44-11-ga9bafd272-dirty` |

## 根因

`renderdoc-mcp 0.2.0` 的 pipeline 工具使用了当前 RenderDoc 1.45 `PipeState` 中不存在的旧式/不匹配接口名。

当前 MCP 调用：

```python
state.GetColorBlend()
state.GetDepthState()
state.GetStencilState()
state.GetRasterizer()
state.GetViewports()
state.GetScissors()
```

RenderDoc 1.45 中实际可用的通用入口包括：

```python
state.GetColorBlends()
state.GetViewport()
state.GetScissor()
state.GetStencilFaces()
```

对 Vulkan capture，更完整和严格的方式是使用 Vulkan 专用状态：

```python
vk = controller.GetVulkanPipelineState()
```

## 解决方案

建议修改 `renderdoc-mcp/src/renderdoc_mcp/tools/pipeline_tools.py` 中固定管线读取逻辑：

1. 对 Vulkan capture 使用 `controller.GetVulkanPipelineState()`。
2. 从 Vulkan 专用结构读取固定管线状态：

```python
vk.colorBlend.blends[i].enabled
vk.colorBlend.blends[i].colorBlend
vk.colorBlend.blends[i].alphaBlend
vk.depthStencil.depthTestEnable
vk.depthStencil.depthWriteEnable
vk.depthStencil.depthFunction
vk.depthStencil.stencilTestEnable
vk.depthStencil.frontFace
vk.depthStencil.backFace
vk.rasterizer.cullMode
vk.rasterizer.frontCCW
vk.rasterizer.fillMode
vk.viewportScissor.viewportScissors[i].vp
vk.viewportScissor.viewportScissors[i].scissor
```

3. 保留通用 `GetPipelineState()` 读取 shader、RT、depth target、vertex input 等跨 API 信息。
4. 后续如需支持 D3D11/D3D12，也应分别走：

```python
controller.GetD3D11PipelineState()
controller.GetD3D12PipelineState()
```

## EID 979 验证结果

直接使用 RenderDoc 1.45 Python API 可读出 EID 979 的固定管线状态：

```text
Blend: 2 个 color attachment，blend 均关闭，writeMask = 15
Depth: test 开启，write 开启，compare = Equal
Stencil: test 关闭
Rasterizer: cull = Back，frontCCW = True，fill = Solid
Viewport: 0,0,2560,1440，depth 0..1
Scissor: 0,0,2560,1440
```

因此该问题不是 capture 缺失状态，而是 MCP 工具层需要适配当前 RenderDoc Python API。
