---
typora-copy-images-to: img
---

# 音像模型

## GPT-SoVITS

指南地址：https://www.yuque.com/baicaigongchang1145haoyuangong/ib3g1e

开源仓库：https://github.com/RVC-Boss/GPT-SoVITS.git



#注意显存，虚拟显存状态下，训练过程将极其缓慢。



# 语言模型

### 介绍

语言模型主要参数：dolphin-llama-3-8B-chinese-Instruct-uncensored-8k-gguf-4bit

开发者：dolphin

名称：llama-3

额外数据集：chinese

作用：Instruct，不适用于直接对话（chat指经过了对话调整的模型）

数据集类型：uncensored，加入了未经审查的数据集

上下文区间：8k

参数量：8B，决定模型的规模和显存占用

模型格式：gguf，对应不同的加载器

量化：4bit





# 基于AI的游戏开发流程



## 游戏程序开发



### Claude Code使用指南

#### 启动命令

| 命令              | 功能             | 示例                          |
| ----------------- | ---------------- | ----------------------------- |
| claude            | 启动交互模式     | claude                        |
| claude -p "任务"  | 快速执行单个任务 | claude -p "创建一个React组件" |
| claude --continue | 继续上次对话     | claude -c                     |
| claude --resume   | 选择历史对话     | claude -r                     |
| claude --help     | 显示帮助信息     | claude -h                     |
| claude --version  | 显示版本信息     | claude -v                     |

#### 高级启动选项

```text
# 跳过权限确认（危险但高效）
claude --dangerously-skip-permissions

# 指定配置文件
claude --config /path/to/config.json

# 调试模式
claude --debug

# 静默模式
claude --quiet

# 指定工作目录
claude --cwd /path/to/project
```

### 会话管理

**常用命令**

| 命令     | 功能            | 说明                    |
| -------- | --------------- | ----------------------- |
| /compact | 压缩对话上下文  | 保留核心信息，节省Token |
| /clear   | 清除对话历史    | 完全重置当前对话        |
| /history | 查看历史对话    | 选择之前的对话继续      |
| /edit    | 编辑记忆文件    | 修改用户或项目记忆      |
| /model   | 切换AI模型      | 选择不同的Claude模型    |
| /help    | 显示帮助        | 查看所有可用命令        |
| /exit    | 退出Claude Code | 返回普通终端            |
|/rewind|回滚上下文||



#### 文件操作

```text
# 分析项目结构
claude "分析这个项目的架构"

# 创建文件
claude "创建一个用户认证的API"

# 修改文件
claude "修复src/utils/auth.js中的bug"

# 批量操作
claude "将所有.js文件转换为TypeScript"
```

------





#### Web缓存机制

**Web 缓存机制详解**
在 Web 环境中，缓存主要分为 静态资源缓存 (文件) 和 数据存储 (状态) 两类。
 1. 静态资源缓存 (浏览器层)
这是浏览器自动处理的，主要用于加速图片、CSS、JS 的加载。

- HTTP 缓存 (Cache-Control) : 服务器告诉浏览器 "这个文件 1 年内不要重新下载"。
- 文件名哈希 (Cache Busting) :
  - 原理 : Vite 构建时会将文件打包为 index.a1b2c.js 。
  - 更新机制 : 当你发布新版本，文件名变成 index.x9y8z.js 。浏览器发现文件名变了，就会强制下载新文件，而旧文件会被自动忽略。这是现代前端（Vue/React）最常用的更新策略。 2. 客户端数据存储 (应用层)
  这是开发者通过代码（JavaScript）主动控制的。

存储方式 特点 适用场景 你的项目应用情况 LocalStorage • 键值对存储 (Key-Value) • 容量约 5MB • 同步 API (会阻塞主线程) • 永久存储 (除非用户手动清除) 简单的用户配置、Token、小型存档。 主要使用 : 你的项目用它来存 SaveManager (Web版存档)、 SettingsManager (设置)、 language (语言)。 SessionStorage • 同 LocalStorage • 临时 : 关闭标签页即消失 临时表单数据、单次会话状态。 暂未使用。 IndexedDB • NoSQL 数据库 • 容量大 (几百 MB甚至更多) • 异步 API (高性能) • 支持索引和事务 复杂的离线应用、大量游戏日志、图片缓存。 暂未使用。如果存档变得非常大（超过5MB），建议迁移到这里。 Cookies • 极小 (4KB) • 每次 HTTP 请求都会携带 身份验证 (Session ID)。 暂未使用。

### Electron 特有的 "缓存" 策略
Electron 本质上是一个运行在桌面上的 Chrome 浏览器，但它拥有 Node.js 能力。

关键区别 :
在纯 Web 端，你只能用 LocalStorage 或 IndexedDB 。但在 Electron 端，你应该优先使用 文件系统 (File System) 。

- 为什么 Electron 不推荐只用 LocalStorage？
  
  1. 易丢失 : 用户如果在 Electron 调试控制台点了 "Clear Storage"，或者卸载重装，LocalStorage 可能会被清空。
  2. 不可见 : 文件存在浏览器的内部目录里，用户找不到存档文件，无法备份或修改。
- 你的项目实践 :
  你的代码 ( src/utils/SaveManager.ts ) 采用了非常标准的 混合适配策略 ：
  
  ```
  if (window.electronAPI) {
    // Electron 环境：调用 IPC 接口，把存档写入硬盘的真实文件 
    (如 .json)
    window.electronAPI.saveGame(slotId, encoded);
  } else {
    // Web 环境：回退到 LocalStorage
    localStorage.setItem(key, encoded);
  }
  ```
  这种设计既保证了 Web 版能跑，又保证了桌面版的数据安全和可访问性。



- 如果遇到代码修改不生效/依赖报错 ：
删除 node_modules\.vite 文件夹，然后重新运行 npm run dev 。
- 如果遇到游戏存档错乱/配置无法重置 ：
删除 %APPDATA%\Let Boss Fly 文件夹（注意：这会 删除所有本地存档 ）。
