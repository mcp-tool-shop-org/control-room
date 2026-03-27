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

**A local-first desktop app for managing scripts, multi-step workflows, and automated operations with full observability, alerting, and self-healing.**

## What is Control Room?

Control Room turns your scripts into observable, repeatable operations. Instead of running `python train.py --config=prod` in a terminal and hoping for the best, you get:

- **Evidence-grade runs** — Every execution is logged with stdout/stderr, exit codes, timing, and SHA256-hashed artifacts
- **Failure fingerprinting** — Recurring errors are grouped and tracked across runs
- **Profiles** — Define preset argument/environment combinations (Smoke, Full, Debug) per script
- **Runbooks** — Multi-step DAG workflows with dependencies, conditions, retry policies, and parallel execution
- **Triggers** — Schedule runbooks on cron, fire them via webhook, or watch files for changes
- **Metrics & Alerts** — Track counters, gauges, histograms, and timers; fire alerts when thresholds breach
- **Health Checks** — Monitor HTTP endpoints, TCP ports, DNS, databases, and Windows services
- **Self-Healing** — Automatically run remediation runbooks when alerts fire
- **AI Assistant** — Ollama-powered error explanation, fix suggestions, and script generation (optional)
- **Command palette** — Keyboard-driven execution with fuzzy search

## Features

### Run Profiles

Define multiple run configurations for each script:

```
Thing: "train-model"
├── Default          (no args)
├── Smoke            --epochs=1 --subset=100
├── Full             --epochs=50 --wandb
└── Debug            --verbose --no-cache  DEBUG=1
```

The command palette shows each profile as a separate action. Retrying a failed run uses the same profile that failed.

### Runbooks

Chain multiple scripts into DAG workflows. Steps execute in parallel where dependencies allow, with configurable conditions:

- **OnSuccess** — run only if all dependencies succeeded
- **OnFailure** — run only if a dependency failed (cleanup/rollback steps)
- **Always** — run regardless of dependency outcome
- **Expression** — custom boolean logic (`step1.succeeded AND step2.failed`)

Each step supports retry policies with exponential backoff, per-step timeouts, and argument overrides.

### Triggers

Automate runbook execution with four trigger types:

- **Manual** — started by user action
- **Schedule** — cron-based recurring execution
- **Webhook** — triggered by HTTP POST with HMAC-SHA256 signature validation
- **File Watch** — triggered when files in a watched directory change (with debounce)

### Failure Groups

Failures are fingerprinted by error signature. The fingerprint algorithm normalizes stderr by stripping timestamps, collapsing GUIDs, memory addresses, file paths, and line numbers before hashing. The Failures page shows recurring issues grouped by fingerprint, with recurrence count and first/last seen timestamps.

### Metrics & Alerts

Track operational metrics (script duration, success/failure rates, system CPU/memory/disk) and define alert rules with conditions like GreaterThan, PercentChange, or Anomaly (Z-score). Alerts support cooldown periods, severity levels (Info/Warning/Error/Critical), and actions (notification, email, webhook, or triggering a runbook).

### Health Checks

Monitor infrastructure with seven check types: HTTP, TCP, DNS, Ping, Script, Database (SQLite), and Windows Service. Each check runs on a configurable interval with timeout, and results roll up into an overall system health status (Healthy/Degraded/Unhealthy).

### Self-Healing

Define rules that match alert conditions and automatically execute remediation runbooks. Built-in patterns cover high CPU, high memory, low disk, and script failure scenarios. Rules support rate limiting, cooldown periods, and optional human approval before execution.

### Timeline

View all runs chronologically. Filter by failure fingerprint to see every occurrence of a specific error.

### ZIP Export

Export any run as a ZIP containing:
- `run-info.json` — Full metadata (args, env, timing, profile used)
- `stdout.txt` / `stderr.txt` — Complete output
- `events.jsonl` — Machine-readable event stream
- `artifacts/` — Any collected artifacts (with SHA256 checksums)

## Tech Stack

- **.NET MAUI** — Cross-platform desktop UI (Windows focus)
- **SQLite (WAL mode)** — Local-first persistence with concurrent read access during runs
- **CommunityToolkit.Mvvm** — MVVM with source generators
- **NCrontab** — Cron expression parsing for scheduled triggers
- **Ollama** — Optional local LLM for AI-powered error analysis and script generation

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
├── ControlRoom.Domain/        # Domain models (Thing, Run, Runbook, Metrics, Alerts)
│   ├── Model/                 # Records: Thing, Run, RunEvent, Artifact, Runbook,
│   │                          #   ThingConfig, RunSummary, Metrics, AlertRule, HealthCheck
│   └── Services/              # IAIAssistant interface
├── ControlRoom.Application/   # Use cases and services
│   ├── UseCases/              # RunLocalScript, RunbookExecutor
│   └── Services/              # AlertEngine, HealthCheckService,
│                              #   SelfHealingEngine, TriggerService
├── ControlRoom.Infrastructure/ # SQLite storage, queries, process runner
│   ├── Storage/               # Db, Migrator, Queries (Thing, Run, Runbook, etc.)
│   ├── Process/               # ScriptRunner (ps1, py, sh, bat auto-detection)
│   └── AI/                    # OllamaAIAssistant
└── ControlRoom.App/           # MAUI UI layer
```

## Security & Data Scope

Control Room is a **local-first** desktop application for script execution and task management.

- **Data accessed:** SQLite database (WAL mode) for run history and task configuration. Executes user-specified scripts. Log files for observability.
- **Data NOT accessed:** No cloud sync. No telemetry. No authentication required. Network access optional.
- **Permissions:** File system read/write for SQLite and logs. Process execution for user scripts. No elevated permissions.

Full policy: [SECURITY.md](SECURITY.md)

---

## Scorecard

| Category | Score |
|----------|-------|
| A. Security | 10/10 |
| B. Error Handling | 10/10 |
| C. Operator Docs | 10/10 |
| D. Shipping Hygiene | 10/10 |
| E. Identity (soft) | 10/10 |
| **Overall** | **50/50** |

---

## License

MIT — see [LICENSE](LICENSE)

## Contributing

Contributions welcome! Please open an issue first to discuss proposed changes.

---

Built by <a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>
