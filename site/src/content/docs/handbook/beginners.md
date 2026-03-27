---
title: Beginner's Guide
description: A step-by-step introduction to Control Room for new users.
sidebar:
  order: 99
---

This guide walks you through the core concepts and first workflows in Control Room, from registering your first script to building automated multi-step pipelines.

## 1. What is Control Room?

Control Room is a local-first desktop application that gives your scripts professional-grade observability. Instead of running scripts in a terminal and losing the output when you close the window, Control Room captures everything: stdout, stderr, exit codes, timing, and artifacts.

Think of it as a personal CI/CD dashboard for your local scripts. You register your scripts once, define how you want to run them (profiles), and every execution is recorded with full evidence. If something fails, you can trace exactly what happened, when, and how often.

Control Room runs entirely on your machine. There is no cloud sync, no telemetry, no account required. Your data stays in a local SQLite database.

## 2. Core concepts

**Thing** -- any script, command, or task you register with Control Room. A Thing points to a file on disk (`.ps1`, `.py`, `.sh`, `.cmd`, `.bat`, or any executable) and holds configuration like working directory and run profiles.

**Profile** -- a named preset of arguments and environment variables for a Thing. For example, your "train-model" script might have a Smoke profile (`--epochs=1`) for quick checks and a Full profile (`--epochs=50 --wandb`) for real training runs.

**Run** -- a single execution of a Thing. Every run records the complete stdout/stderr, exit code, timing, which profile was used, and any artifacts the script produced.

**Runbook** -- a multi-step workflow that chains multiple Things together in a directed acyclic graph (DAG). Steps can run in parallel when their dependencies allow, and you can define conditions like "only run the cleanup step if the build step failed."

**Trigger** -- an automation rule that starts a runbook automatically. Triggers can fire on a cron schedule, in response to a webhook, or when files in a watched directory change.

**Alert** -- a rule that monitors metrics (like script failure rate or system CPU) and fires when a threshold is breached. Alerts have severity levels and can trigger notifications or even self-healing runbooks.

**Health Check** -- a periodic probe that monitors infrastructure: HTTP endpoints, TCP ports, DNS resolution, databases, or Windows services. Results roll up into an overall system health status.

## 3. Registering your first Thing

To start using Control Room, register a script:

1. Open Control Room and navigate to the Things view.
2. Click "Add Thing" and point it to a script file on disk.
3. Control Room auto-detects the launcher:
   - `.ps1` files run with `pwsh -NoProfile -ExecutionPolicy Bypass`
   - `.py` files run with `python`
   - `.sh` files run with `bash`
   - `.cmd` / `.bat` files run with `cmd.exe /c`
   - Other files execute directly
4. Optionally set a working directory (defaults to the script's directory).
5. Save. Your Thing now appears in the Things list and the command palette.

## 4. Running and reviewing

Once you have a Thing registered:

1. Open the command palette (keyboard shortcut) or click Run from the Things list.
2. Pick a profile (or use Default if you have not defined any).
3. Watch stdout and stderr stream in real time.
4. When the run completes, review the result:
   - **Exit code** -- 0 means success, non-zero means failure.
   - **Duration** -- how long the execution took.
   - **Artifacts** -- any files your script wrote to `CONTROLROOM_RUN_DIR` are automatically captured with SHA256 checksums.
   - **Failure fingerprint** -- if the run failed, the error output is normalized and hashed. This fingerprint groups recurring identical failures together.

Every run is stored permanently. You can go back to any past execution and see exactly what happened.

## 5. Setting up profiles

Profiles save you from remembering command-line flags:

1. Open a Thing's detail view.
2. Add a profile with a name (e.g., "Smoke").
3. Set the arguments (e.g., `--epochs=1 --subset=100`).
4. Add environment variables if needed (e.g., `DEBUG=1`).
5. Optionally override the working directory.
6. Save.

Now the command palette shows your Thing with each profile as a separate action. When you retry a failed run, it automatically uses the same profile that failed, ensuring reproducibility.

## 6. Building a runbook

Runbooks let you chain scripts into workflows:

1. Create a new Runbook and give it a name and description.
2. Add steps, each pointing to a Thing and a profile.
3. Define dependencies between steps. A step only starts when all its dependencies are complete.
4. Set conditions on each step:
   - **OnSuccess** -- run only if all dependencies succeeded (default for most steps).
   - **OnFailure** -- run only if a dependency failed (useful for cleanup or rollback).
   - **Always** -- run regardless of outcome.
   - **Expression** -- custom logic like `build.succeeded AND lint.succeeded`.
5. Optionally add a retry policy (max attempts, backoff delay) and a timeout per step.
6. Save and run manually, or attach a trigger.

Steps with independent dependencies execute in parallel automatically. The runbook executor resolves the DAG and runs steps in waves.

## 7. Automating with triggers and alerts

Once your runbooks are working, automate them:

**Triggers** start runbooks without manual intervention:
- **Schedule** -- use a cron expression (e.g., `0 9 * * 1-5` for weekdays at 9 AM).
- **Webhook** -- send an HTTP POST with an HMAC-SHA256 signed payload to fire a runbook.
- **File Watch** -- trigger when files in a directory change (with configurable debounce).

**Alerts** monitor metrics and fire when thresholds breach:
- Define an alert rule with a metric name, condition (e.g., GreaterThan), threshold, and severity.
- Alerts evaluate every 15 seconds with cooldown periods to prevent storms.
- Actions include in-app notification, email, webhook, or running a remediation runbook.

**Self-Healing** closes the loop by automatically running remediation runbooks when alerts fire:
- Built-in patterns cover high CPU, high memory, low disk space, and script failures.
- Rules support rate limiting (max executions per hour) and cooldown periods.
- Critical actions can require human approval before execution.

This combination of triggers, alerts, and self-healing lets you build fully automated operational pipelines that respond to both scheduled events and real-time conditions.
