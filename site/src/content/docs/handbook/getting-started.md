---
title: Getting Started
description: Build and run Control Room for observable script execution on Windows.
sidebar:
  order: 1
---

Control Room turns your scripts into observable, repeatable operations with evidence-grade logging.

## Prerequisites

- .NET 10 SDK
- Windows 10 or Windows 11

## Build and run

```bash
dotnet restore
dotnet build
dotnet run --project ControlRoom.App
```

## First steps

1. **Add a Thing** — register a script, command, or task you want to manage
2. **Create profiles** — define argument and environment presets (Smoke, Full, Debug)
3. **Run it** — execute from the command palette or the Things list
4. **Review** — check stdout/stderr, exit codes, timing, and artifacts in the run detail view

## What gets captured

Every execution records:
- Complete stdout and stderr streams
- Process exit code with success/failure classification
- Start time, end time, and duration
- Which profile was used
- Any collected artifacts
- Failure fingerprint (if the run failed)
