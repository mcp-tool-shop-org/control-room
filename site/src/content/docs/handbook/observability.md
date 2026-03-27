---
title: Observability & Failure Fingerprinting
description: How Control Room captures evidence, groups recurring failures, tracks metrics, and fires alerts.
sidebar:
  order: 3
---

Control Room treats every run as evidence. Nothing is lost.

## Evidence-grade runs

Every execution captures:

| Data | Description |
|------|-------------|
| stdout / stderr | Complete output streams in real time |
| Exit code | Process exit code with success/failure classification |
| Timing | Start time, end time, and duration |
| Profile | Which argument/environment preset was used |
| Artifacts | Any files written to `CONTROLROOM_RUN_DIR`, captured with SHA256 hash and media type |
| Fingerprint | Error signature for grouping recurring issues |
| Command line | Resolved command for reproducibility |
| Environment | Profile env overrides recorded in the run summary |

## Failure fingerprinting

When a run fails, Control Room computes a fingerprint from the exit code and normalized stderr. The normalization process:

1. Takes the last 50 lines of stderr (most relevant for stack traces)
2. Strips timestamps (ISO 8601 and common patterns)
3. Collapses GUIDs, hex addresses, and memory addresses
4. Normalizes Windows and Unix file paths
5. Collapses line numbers in stack traces
6. Strips process IDs
7. Hashes the result with SHA256

Recurring errors with the same fingerprint are grouped together. The Failures page shows:
- Grouped failures by fingerprint
- Recurrence count per fingerprint
- First seen and last seen timestamps
- Quick navigation to individual failed runs

This helps you distinguish between new failures and known recurring issues.

## Metrics

Control Room tracks operational metrics across four types:

| Type | Description |
|------|-------------|
| Counter | Monotonically increasing value (e.g., total script runs) |
| Gauge | Point-in-time value (e.g., current CPU percent) |
| Histogram | Distribution of values |
| Timer | Duration measurement (e.g., script execution time) |

Built-in metric names cover script execution (duration, success, failure, output size, exit code), runbook execution (duration, steps total/failed/skipped), system resources (CPU, memory, disk, process count), trigger activity (fired count, latency), and queue depth.

Metrics support tags for filtering (thing_id, runbook_id, step_id, status, host, etc.) and can be aggregated over time windows with min, max, avg, P50, P90, P99, and standard deviation.

## Alerts

Alert rules evaluate metrics against thresholds on a 15-second cycle. Supported conditions:

| Condition | Meaning |
|-----------|---------|
| GreaterThan / LessThan | Simple threshold comparison |
| AbsoluteChange | Value changed by more than N |
| PercentChange | Value changed by more than N% |
| Anomaly | Z-score exceeds threshold (statistical outlier) |

Each rule has a severity (Info, Warning, Error, Critical), an evaluation window, and a cooldown period to prevent alert storms. When an alert fires, it can trigger actions: in-app notification, email, webhook POST, script execution, or running a remediation runbook.

Alerts auto-resolve when the condition clears. Active alerts can also be manually acknowledged or resolved.

## Timeline

The Timeline view shows all runs chronologically. Filter by:
- Thing name
- Profile used
- Success or failure status
- Failure fingerprint -- see every occurrence of a specific error

## Why this matters

Running scripts in a terminal loses history. Control Room keeps a complete record of every execution so you can answer questions like:
- When did this error first appear?
- How often does this failure recur?
- What arguments were used in the last successful run?
- Has this script been getting slower over time?
- Which system metrics were anomalous when this job failed?
