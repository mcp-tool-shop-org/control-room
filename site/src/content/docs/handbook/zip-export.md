---
title: ZIP Export & Artifacts
description: Export any run as a portable evidence bundle and understand how artifacts are captured.
sidebar:
  order: 4
---

Control Room can export any run as a self-contained ZIP archive.

## What is included

| File | Contents |
|------|----------|
| run-info.json | Full metadata -- args, env, timing, profile used, resolved command line |
| stdout.txt | Complete standard output |
| stderr.txt | Complete standard error |
| events.jsonl | Machine-readable event stream |
| artifacts/ | Any collected output artifacts |

## Artifact capture

During execution, Control Room creates a dedicated run directory at `%LOCALAPPDATA%/ControlRoom/runs/<run-id>/`. The path is exposed to your script via the `CONTROLROOM_RUN_DIR` environment variable.

Any files your script writes to this directory are automatically captured as artifacts when the run completes. Each artifact records:

- **Media type** -- auto-detected from file extension (json, csv, png, pdf, etc.)
- **SHA256 hash** -- cryptographic checksum for integrity verification
- **Locator** -- full file path on disk
- **Creation timestamp**

## How to export

1. Open the run detail view
2. Click the export button
3. Choose a save location
4. The ZIP is created with all run data

## Use cases

- **Sharing with teammates** -- send a ZIP instead of pasting terminal output
- **Bug reports** -- attach the run evidence to an issue
- **Archival** -- keep a permanent record of important executions
- **Auditing** -- prove what ran, when, and what the output was
- **Debugging** -- compare the event stream from a failed run against a successful one
- **Reproducibility** -- the resolved command line and environment are captured so you can re-run exactly

## Machine-readable events

The `events.jsonl` file contains a structured event stream for programmatic analysis. Each line is a JSON object with a timestamp, event type, and payload. Event types include:

| Event | Payload |
|-------|---------|
| RunStarted | Thing name, profile ID and name |
| StdOut | Output line |
| StdErr | Error line |
| StatusChanged | New status |
| ArtifactAdded | Artifact metadata |
| RunEnded | Status, exit code, duration, line counts, artifact count, failure fingerprint, profile info |

This enables automated processing of run data outside of Control Room.
