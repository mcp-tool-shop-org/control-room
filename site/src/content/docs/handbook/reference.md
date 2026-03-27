---
title: Reference
description: Technical reference for Control Room — architecture, domain model, tech stack, and project structure.
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
| Cron Parsing | NCrontab |
| AI (optional) | Ollama for local LLM inference |
| Architecture | Clean Architecture (Domain / Application / Infrastructure / App) |

## Project structure

```
ControlRoom/
├── ControlRoom.Domain/
│   ├── Model/        Thing, Run, RunEvent, Artifact, ThingConfig,
│   │                 Runbook, RunbookStep, RunbookExecution,
│   │                 Metrics, AlertRule, Alert, HealthCheck,
│   │                 SelfHealingRule, RunSummary, Enums, Ids
│   └── Services/     IAIAssistant interface
├── ControlRoom.Application/
│   ├── UseCases/     RunLocalScript, RunbookExecutor
│   └── Services/     AlertEngine, HealthCheckService,
│                     SelfHealingEngine, TriggerService
├── ControlRoom.Infrastructure/
│   ├── Storage/      Db, Migrator, Queries/*
│   ├── Process/      ScriptRunner
│   └── AI/           OllamaAIAssistant
└── ControlRoom.App/  MAUI UI (Pages, Converters, Shell)
```

## Domain concepts

| Concept | Description |
|---------|-------------|
| **Thing** | A script, command, or task registered for management |
| **ThingConfig** | JSON configuration for a Thing: script path, working directory, and profiles (schema-versioned, auto-migrated) |
| **ThingProfile** | A named set of arguments and environment variables for a Thing |
| **Run** | A single execution of a Thing with captured output, exit code, timing, and summary |
| **RunEvent** | A timestamped event in a run's lifecycle (RunStarted, StdOut, StdErr, StatusChanged, ArtifactAdded, RunEnded) |
| **RunSummary** | Rich execution summary including duration, line counts, failure fingerprint, resolved command line, profile used, and env overrides |
| **Artifact** | A file produced during a run, captured with media type detection and SHA256 hash |
| **Failure fingerprint** | SHA256 of normalized stderr (timestamps, GUIDs, addresses, paths, and line numbers stripped) combined with exit code |
| **Runbook** | A multi-step DAG workflow with named steps, dependencies, conditions, retry policies, and triggers |
| **RunbookStep** | A single step in a runbook, referencing a Thing and profile with optional timeout and argument override |
| **StepCondition** | Execution condition: Always, OnSuccess, OnFailure, or Expression (boolean logic like `step1.succeeded AND step2.failed`) |
| **RetryPolicy** | Retry configuration with max attempts, initial delay, backoff multiplier, and max delay |
| **RunbookTrigger** | How a runbook is started: Manual, Schedule (cron), Webhook (HMAC-SHA256), or FileWatch (with debounce) |
| **MetricPoint** | A single metric data point with name, type (Counter/Gauge/Histogram/Timer), value, timestamp, and tags |
| **AlertRule** | A rule that evaluates metrics against a threshold with a condition operator, evaluation window, cooldown, severity, and actions |
| **HealthCheck** | A periodic check (HTTP, TCP, DNS, Ping, Script, Database, Service) with interval and timeout |
| **SelfHealingRule** | A rule that matches alert conditions and triggers a remediation runbook, with rate limiting and optional approval |

## Run statuses

| Status | Meaning |
|--------|---------|
| Running | Execution is in progress |
| Succeeded | Exited with code 0 |
| Failed | Exited with non-zero code |
| Canceled | Canceled by user or timeout |

## Runbook execution statuses

| Status | Meaning |
|--------|---------|
| Pending | Not yet started |
| Running | Steps are executing |
| Paused | Paused by user (can resume) |
| Succeeded | All steps succeeded |
| Failed | One or more steps failed with no successes |
| Canceled | Canceled by user |
| PartialSuccess | Some steps succeeded, some failed |

## Script launcher auto-detection

The ScriptRunner resolves the launcher based on file extension:

| Extension | Launcher | Notes |
|-----------|----------|-------|
| `.ps1` | `pwsh` | Runs with `-NoProfile -ExecutionPolicy Bypass` |
| `.cmd` / `.bat` | `cmd.exe` | Runs with `/c` |
| `.py` | `python` | |
| `.sh` | `bash` | |
| Other | Direct execution | Uses the file itself as the executable |

## Environment variables injected per run

| Variable | Value |
|----------|-------|
| `CONTROLROOM_RUN_DIR` | Path to the run's evidence directory |
| `CONTROLROOM_ARTIFACT_DIR` | Same as RUN_DIR (legacy compatibility) |
| `CONTROLROOM_RUN_ID` | GUID of the current run |
| `CONTROLROOM_PROFILE_ID` | ID of the profile being used |
| `CONTROLROOM_PROFILE_NAME` | Display name of the profile |

Scripts can write files to `CONTROLROOM_RUN_DIR` and they will be captured as artifacts automatically.

## SQLite WAL mode

Control Room uses SQLite in Write-Ahead Logging mode for concurrent read access during active runs. The database is fully local -- no network access required. Foreign keys are enabled, and synchronous mode is set to NORMAL for a balance of safety and performance.

## Building from source

```bash
# Prerequisites: .NET 10 SDK, Windows 10/11
dotnet restore
dotnet build
dotnet run --project ControlRoom.App
```

## Security

- Local-first -- all data stays on your machine
- No cloud sync, no telemetry, no analytics
- Process execution is user-initiated only (or via configured triggers)
- Webhook triggers validate HMAC-SHA256 signatures
- See [SECURITY.md](https://github.com/mcp-tool-shop-org/control-room/blob/main/SECURITY.md) for vulnerability reporting
