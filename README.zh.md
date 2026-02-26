<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/mcp-tool-shop-org/control-room/main/assets/logo-control-room.png" alt="Control Room" width="400">
</p>

<p align="center">
  <a href="https://github.com/mcp-tool-shop-org/control-room/actions/workflows/ci.yml"><img src="https://github.com/mcp-tool-shop-org/control-room/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow" alt="MIT License"></a>
  <a href="https://mcp-tool-shop-org.github.io/control-room/"><img src="https://img.shields.io/badge/Landing_Page-live-blue" alt="Landing Page"></a>
</p>

**一款以本地优先为原则的桌面应用程序，用于管理和执行脚本、服务器和任务，并提供全面的可观察性。**

## 什么是控制中心？

控制中心将您的脚本转化为可观察、可重复的操作。与其在终端中运行 `python train.py --config=prod` 并希望一切顺利，您将获得：

- **高质量的执行记录** — 每次执行都会记录标准输出/标准错误、退出码、运行时间和相关文件。
- **错误指纹识别** — 经常出现的错误会被分组并跟踪，以便进行分析。
- **配置方案** — 为每个脚本定义预设的参数/环境组合（例如：测试、完整、调试）。
- **命令面板** — 通过键盘驱动的执行，并支持模糊搜索。

## 功能

### 配置方案（新功能！）

为每个脚本定义多个运行配置：

```
Thing: "train-model"
├── Default          (no args)
├── Smoke            --epochs=1 --subset=100
├── Full             --epochs=50 --wandb
└── Debug            --verbose --no-cache  DEBUG=1
```

命令面板会显示每个配置方案作为一个独立的动作。 重新运行失败的执行将使用导致失败的相同配置方案。

### 错误分组

错误通过错误签名进行指纹识别。 “错误”页面显示重复出现的错误，并按指纹进行分组，包括出现次数和首次/最后出现的时间戳。

### 时间轴

按时间顺序查看所有执行记录。 通过错误指纹进行过滤，以查看特定错误的每次出现。

### ZIP 导出

将任何执行记录导出为 ZIP 文件，其中包含：
- `run-info.json` — 完整的元数据（参数、环境、运行时间、使用的配置方案）。
- `stdout.txt` / `stderr.txt` — 完整的输出。
- `events.jsonl` — 机器可读的事件流。
- `artifacts/` — 收集到的所有相关文件。

## 技术栈

- **.NET MAUI** — 跨平台桌面用户界面（主要针对 Windows）。
- **SQLite (WAL 模式)** — 本地优先的数据持久化。
- **CommunityToolkit.Mvvm** — 基于源代码生成器的 MVVM 框架。

## 入门

### 先决条件

- .NET 10 SDK
- Windows 10/11

### 构建

```bash
dotnet restore
dotnet build
```

### 运行

```bash
dotnet run --project ControlRoom.App
```

## 项目结构

```
ControlRoom/
├── ControlRoom.Domain/        # Domain models (Thing, Run, ThingConfig, etc.)
├── ControlRoom.Application/   # Use cases (RunLocalScript, etc.)
├── ControlRoom.Infrastructure/ # SQLite storage, queries
└── ControlRoom.App/           # MAUI UI layer
```

## 许可证

MIT — 参见 [LICENSE](LICENSE)

## 贡献

欢迎贡献！ 请先提出问题，讨论您提出的更改。
