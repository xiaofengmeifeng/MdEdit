# MdEdit

一款小巧的 Windows Markdown 编辑器（C# / .NET 8 / WinForms）——左侧编辑、右侧实时预览，支持代码高亮、目录、多文档、查找替换与单实例唤起。

> 预览区是一个内嵌的 Chromium（WebView2）页面，Markdown 由 Markdig 解析为 HTML 后渲染，代码高亮与交互脚本全部离线内嵌，无需联网。

## 功能特性

- **实时预览**：左侧编写 Markdown，右侧即时渲染（GFM 表格、任务列表、删除线、Emoji、脚注等）。
- **代码高亮**：内嵌 highlight.js，覆盖常用语言及 PowerShell、Dockerfile、nginx、YAML、CMake 等。
- **Mermaid 图表**：```mermaid 代码块（流程图、时序图等）会在预览中渲染为图形。
- **代码块一键复制**：预览中每个代码块右上角带复制按钮。
- **多标签**：顶部标签栏可同时打开多个文档并自由切换，每个标签带关闭按钮与未保存标记。
- **目录（TOC）**：右侧按标题层级生成目录，可点击跳转、可拖动调整宽度、可收起/展开。
- **查找与替换**：编辑器内 `Ctrl+F` / `Ctrl+R`，支持区分大小写、全字匹配、正则表达式与全部替换。
- **预览内查找**：右侧搜索框同样支持大小写 / 全字 / 正则，匹配项黄底高亮、当前项橙色，可上一个/下一个。
- **文件树**：可打开文件夹，树形浏览并支持按文件名过滤；右键可打开、重命名、删除、在资源管理器中显示。
- **视图模式**：仅编辑 / 分栏 / 仅预览，自动换行开关。
- **会话恢复**：再次启动时还原上次打开的文件与目录。
- **外部修改重载**：文件被其它程序修改并保存后，切回 MdEdit 会自动重新加载；也可按 `F5`、工具栏的刷新按钮或预览区的刷新按钮手动重新加载。
- **单实例**：程序已在运行时，双击其它 `md` 文件会在现有窗口中打开，不会重复启动。

## 技术栈

| 用途 | 方案 |
| --- | --- |
| 语言 / 运行时 | C# / .NET 8 (`net8.0-windows`) |
| 桌面 UI | Windows Forms |
| 预览渲染 | WebView2（Chromium / Edge） |
| Markdown 解析 | [Markdig](https://github.com/xoofx/markdig) |
| 代码高亮 | [highlight.js](https://highlightjs.org/) |
| 图标 | [Font Awesome Free](https://fontawesome.com/)（图标字体，运行时渲染为位图） |

## 目录结构

```
MdEditor/
├─ Assets/                     # 内嵌资源（随 exe 打包）
│  ├─ app.ico                  # 应用图标
│  ├─ fa-solid-900.ttf         # 图标字体
│  ├─ hljs-all.min.js          # highlight.js
│  └─ hljs-github.min.css      # 代码高亮样式
├─ Controls/
│  └─ RoundedPanel.cs          # 圆角输入框外框
├─ Forms/
│  ├─ MainForm.cs              # 主窗体：字段 / 生命周期
│  ├─ MainForm.Layout.cs       # 界面构建
│  ├─ MainForm.Documents.cs    # 多文档与会话
│  ├─ MainForm.Tree.cs         # 文件夹树与过滤
│  ├─ MainForm.Editor.cs       # 编辑 / 格式化 / 查找替换
│  ├─ MainForm.Preview.cs      # 预览 / 目录 / 视图模式
│  └─ PromptDialog.cs          # 简易输入对话框
├─ Models/
│  ├─ OpenDocument.cs          # 打开的文档模型
│  ├─ SessionData.cs           # 会话记录
│  ├─ TocEntry.cs              # 目录条目
│  └─ ViewMode.cs              # 视图模式枚举
├─ Services/
│  ├─ AppPaths.cs              # 应用路径
│  ├─ AssetService.cs          # 内嵌资源与图标渲染
│  ├─ MarkdownService.cs       # Markdown 解析与目录提取
│  ├─ PreviewDocument.cs       # 预览页 HTML 外壳（样式 + 脚本）
│  ├─ SessionService.cs        # 会话持久化
│  └─ IpcService.cs            # 单实例 IPC（命名管道）
├─ AppConstants.cs             # 常量
├─ CodeLanguages.cs            # 可插入的代码语言
├─ Program.cs                  # 入口（单实例 + IPC 客户端）
├─ MdEditor.csproj
└─ MdEditor.sln
```

## 环境要求

- Windows 10/11
- [.NET 8 桌面运行时](https://dotnet.microsoft.com/download/dotnet/8.0)（Desktop Runtime）
- [WebView2 运行时](https://developer.microsoft.com/microsoft-edge/webview2/)（Windows 10/11 通常已预装）

## 构建与运行

```bash
# 克隆
git clone <your-repo-url>
cd MdEditor

# 运行（开发）
dotnet run --project MdEditor.csproj

# 构建
dotnet build -c Release

# 发布为单文件 exe（框架依赖）
dotnet publish MdEditor.csproj -c Release -o publish
```

发布产物为 `publish/MdEdit.exe`（单文件，约 3 MB，需上述运行时）。也可以直接用系统默认程序打开某个文件：

```bash
MdEdit.exe "D:\docs\README.md"
```

## 快捷键

| 快捷键 | 功能 |
| --- | --- |
| `Ctrl + N` | 新建 |
| `Ctrl + O` | 打开文件 |
| `Ctrl + K` | 打开文件夹 |
| `Ctrl + S` | 保存 |
| `Ctrl + Shift + S` | 另存为 |
| `F5` | 从磁盘重新加载当前文档 |
| `Ctrl + F` | 查找 |
| `Ctrl + R` | 替换 |
| `Ctrl + B` / `Ctrl + I` | 粗体 / 斜体 |
| `F3` / `Shift + F3` | 下一个 / 上一个匹配 |
| `Ctrl + 1` / `Ctrl + 2` / `Ctrl + 3` | 仅编辑 / 分栏 / 仅预览 |

## 第三方资源与许可

本项目基于 MIT 许可发布（见 [LICENSE](LICENSE)）。内嵌的第三方资源遵循各自许可：

- [Markdig](https://github.com/xoofx/markdig) — BSD-2-Clause
- [highlight.js](https://github.com/highlightjs/highlight.js) — BSD-3-Clause
- [Font Awesome Free](https://fontawesome.com/license/free) — 图标 CC BY 4.0，字体 SIL OFL 1.1
- [WebView2](https://learn.microsoft.com/microsoft-edge/webview2/) — Microsoft 软件许可条款

## 贡献

欢迎提交 Issue 与 Pull Request。
