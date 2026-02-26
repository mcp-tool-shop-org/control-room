<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/control-room/readme.png" alt="Control Room" width="400">
</p>

<p align="center">
  <a href="https://github.com/mcp-tool-shop-org/control-room/actions/workflows/ci.yml"><img src="https://github.com/mcp-tool-shop-org/control-room/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow" alt="MIT License"></a>
  <a href="https://mcp-tool-shop-org.github.io/control-room/"><img src="https://img.shields.io/badge/Landing_Page-live-blue" alt="Landing Page"></a>
</p>

**A local-first desktop app for managing and executing scripts, servers, and tasks with full observability.**

## What is Control Room?

Control Room turns your scripts into observable, repeatable operations. Instead of running `python train.py --config=prod` in a terminal and hoping for the best, you get:

- **Evidence-grade runs** — Every execution is logged with stdout/stderr, exit codes, timing, and artifacts
- **Failure fingerprinting** — Recurring errors are grouped and tracked across runs
- **Profiles** — Define preset argument/environment combinations (Smoke, Full, Debug) per script
- **Command palette** — Keyboard-driven execution with fuzzy search

## Features

### Profiles (New!)

Define multiple run configurations for each script:

```
Thing: "train-model"
├── Default          (no args)
├── Smoke            --epochs=1 --subset=100
├── Full             --epochs=50 --wandb
└── Debug            --verbose --no-cache  DEBUG=1
```

The command palette shows each profile as a separate action. Retrying a failed run uses the same profile that failed.

### Failure Groups

Failures are fingerprinted by error signature. The Failures page shows recurring issues grouped by fingerprint, with recurrence count and first/last seen timestamps.

### Timeline

View all runs chronologically. Filter by failure fingerprint to see every occurrence of a specific error.

### ZIP Export

Export any run as a ZIP containing:
- `run-info.json` — Full metadata (args, env, timing, profile used)
- `stdout.txt` / `stderr.txt` — Complete output
- `events.jsonl` — Machine-readable event stream
- `artifacts/` — Any collected artifacts

## Tech Stack

- **.NET MAUI** — Cross-platform desktop UI (Windows focus)
- **SQLite (WAL mode)** — Local-first persistence
- **CommunityToolkit.Mvvm** — MVVM with source generators

## Getting Started

### Prerequisites

- .NET 10 SDK
- Windows 10/11

### Build

```bash
dotnet restore
dotnet build
```

### Run

```bash
dotnet run --project ControlRoom.App
```

## Project Structure

```
ControlRoom/
├── ControlRoom.Domain/        # Domain models (Thing, Run, ThingConfig, etc.)
├── ControlRoom.Application/   # Use cases (RunLocalScript, etc.)
├── ControlRoom.Infrastructure/ # SQLite storage, queries
└── ControlRoom.App/           # MAUI UI layer
```

## License

MIT — see [LICENSE](LICENSE)

## Contributing

Contributions welcome! Please open an issue first to discuss proposed changes.
