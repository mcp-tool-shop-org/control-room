---
title: Reference
description: Technical reference for Control Room — architecture, tech stack, and project structure.
sidebar:
  order: 5
---

## Tech stack

| Layer | Technology |
|-------|-----------|
| UI Framework | .NET MAUI (Windows focus) |
| Runtime | .NET 10 |
| MVVM | CommunityToolkit.Mvvm with source generators |
| Storage | SQLite (WAL mode) |
| Architecture | Clean Architecture |

## Project structure

```
ControlRoom/
├── ControlRoom.Domain/        # Domain models (Thing, Run, ThingConfig)
├── ControlRoom.Application/   # Use cases (RunLocalScript, etc.)
├── ControlRoom.Infrastructure/ # SQLite storage, queries
└── ControlRoom.App/           # MAUI UI layer
```

## Domain concepts

- **Thing** — a script, command, or task registered for management
- **Run** — a single execution of a Thing with captured output and metadata
- **Profile** — a named set of arguments and environment variables for a Thing
- **Failure fingerprint** — an error signature extracted from failed runs for grouping

## SQLite WAL mode

Control Room uses SQLite in Write-Ahead Logging mode for concurrent read access during active runs. The database is fully local — no network access required.

## Building from source

```bash
# Prerequisites: .NET 10 SDK, Windows 10/11
dotnet restore
dotnet build
dotnet run --project ControlRoom.App
```

## Security

- Local-first — all data stays on your machine
- No cloud sync, no telemetry, no analytics
- Process execution is user-initiated only
- See [SECURITY.md](https://github.com/mcp-tool-shop-org/control-room/blob/main/SECURITY.md) for vulnerability reporting
