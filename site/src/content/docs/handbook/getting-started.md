---
title: Getting Started
description: Build and run Control Room for observable script execution and workflow automation on Windows.
sidebar:
  order: 1
---

Control Room turns your scripts into observable, repeatable operations with evidence-grade logging, multi-step workflows, and automated monitoring.

## Prerequisites

- .NET 10 SDK
- Windows 10 or Windows 11
- Ollama (optional, for AI-powered error analysis)

## Build and run

```bash
dotnet restore
dotnet build
dotnet run --project ControlRoom.App
```

## First steps

1. **Add a Thing** -- register a script, command, or task you want to manage. Control Room auto-detects the launcher for `.ps1`, `.py`, `.sh`, `.cmd`, and `.bat` files.
2. **Create profiles** -- define argument and environment presets (Smoke, Full, Debug) for different execution modes.
3. **Run it** -- execute from the command palette or the Things list. Pick a profile or use the default.
4. **Review** -- check stdout/stderr, exit codes, timing, and artifacts in the run detail view.
5. **Build runbooks** -- chain multiple scripts into DAG workflows with dependencies, conditions, and retry policies.
6. **Set up triggers** -- automate runbook execution with cron schedules, webhooks, or file watchers.
7. **Monitor** -- define health checks and alert rules to track system and script health over time.

## What gets captured

Every execution records:
- Complete stdout and stderr streams (line by line, in real time)
- Process exit code with success/failure classification
- Start time, end time, and duration
- Which profile was used (ID, name, resolved args, env overrides)
- The resolved command line for reproducibility
- Any files written to `CONTROLROOM_RUN_DIR`, captured as artifacts with SHA256 hashes
- Failure fingerprint (if the run failed) for grouping recurring errors

## Where data lives

All data is stored locally in SQLite (WAL mode) under your local application data directory. No cloud sync, no telemetry. Run evidence directories are stored at `%LOCALAPPDATA%/ControlRoom/runs/<run-id>/`.
