---
title: Run Profiles
description: Define preset argument and environment combinations for repeatable script execution.
sidebar:
  order: 2
---

Profiles let you define multiple run configurations for each script.

## What is a profile?

A profile is a named combination of arguments and environment variables. Instead of remembering command-line flags, you define them once and select by name.

## Example

```
Thing: "train-model"
├── Default          (no args)
├── Smoke            --epochs=1 --subset=100
├── Full             --epochs=50 --wandb
└── Debug            --verbose --no-cache  DEBUG=1
```

## Using profiles

- Each profile appears as a separate action in the command palette
- Select a Thing, then pick which profile to run
- Retrying a failed run automatically uses the same profile that failed
- Profile metadata is captured in every run record

## Creating profiles

1. Open the Thing detail view
2. Add a new profile with a name
3. Specify arguments (command-line flags)
4. Specify environment variables (key=value pairs)
5. Save — the profile is now available in the command palette

## When to use profiles

- **Smoke** — quick validation with minimal data, fast feedback
- **Full** — production-grade execution with all flags enabled
- **Debug** — verbose logging, no caching, diagnostic environment variables
- **Custom** — any combination specific to your workflow
