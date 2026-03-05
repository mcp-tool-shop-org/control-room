---
title: ZIP Export
description: Export any run as a portable evidence bundle for sharing or archival.
sidebar:
  order: 4
---

Control Room can export any run as a self-contained ZIP archive.

## What is included

| File | Contents |
|------|----------|
| run-info.json | Full metadata — args, env, timing, profile used |
| stdout.txt | Complete standard output |
| stderr.txt | Complete standard error |
| events.jsonl | Machine-readable event stream |
| artifacts/ | Any collected output artifacts |

## How to export

1. Open the run detail view
2. Click the export button
3. Choose a save location
4. The ZIP is created with all run data

## Use cases

- **Sharing with teammates** — send a ZIP instead of pasting terminal output
- **Bug reports** — attach the run evidence to an issue
- **Archival** — keep a permanent record of important executions
- **Auditing** — prove what ran, when, and what the output was
- **Debugging** — compare the event stream from a failed run against a successful one

## Machine-readable events

The `events.jsonl` file contains a structured event stream for programmatic analysis. Each line is a JSON object with a timestamp, event type, and payload. This enables automated processing of run data outside of Control Room.
