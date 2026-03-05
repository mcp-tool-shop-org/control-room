---
title: Observability & Failure Fingerprinting
description: How Control Room captures evidence and groups recurring failures.
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
| Artifacts | Any collected output files |
| Fingerprint | Error signature for grouping recurring issues |

## Failure fingerprinting

When a run fails, Control Room extracts a fingerprint from the error output. Recurring errors with the same fingerprint are grouped together.

The Failures page shows:
- Grouped failures by fingerprint
- Recurrence count per fingerprint
- First seen and last seen timestamps
- Quick navigation to individual failed runs

This helps you distinguish between new failures and known recurring issues.

## Timeline

The Timeline view shows all runs chronologically. Filter by:
- Thing name
- Profile used
- Success or failure status
- Failure fingerprint — see every occurrence of a specific error

## Why this matters

Running scripts in a terminal loses history. Control Room keeps a complete record of every execution so you can answer questions like:
- When did this error first appear?
- How often does this failure recur?
- What arguments were used in the last successful run?
- Has this script been getting slower over time?
