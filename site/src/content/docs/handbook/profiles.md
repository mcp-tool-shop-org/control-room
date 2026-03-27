---
title: Run Profiles
description: Define preset argument and environment combinations for repeatable script execution.
sidebar:
  order: 2
---

Profiles let you define multiple run configurations for each script.

## What is a profile?

A profile is a named combination of arguments, environment variables, and an optional working directory override. Instead of remembering command-line flags, you define them once and select by name.

## Example

```
Thing: "train-model"
├── Default          (no args)
├── Smoke            --epochs=1 --subset=100
├── Full             --epochs=50 --wandb
└── Debug            --verbose --no-cache  DEBUG=1
```

## Profile configuration

Each profile is stored as part of the Thing's JSON configuration (schema version 2). A profile contains:

| Field | Description |
|-------|-------------|
| id | Unique identifier (e.g., `smoke`, `full`) |
| name | Display name shown in the command palette |
| args | Command-line arguments passed to the script |
| env | Environment variable overrides (key-value pairs) |
| workingDir | Optional working directory override (falls back to Thing default, then script directory) |

## Using profiles

- Each profile appears as a separate action in the command palette
- Select a Thing, then pick which profile to run
- Retrying a failed run automatically uses the same profile that failed
- Profile metadata is captured in every run record (profile ID, name, resolved args, and env overrides)
- If no profiles are defined, a Default profile is used automatically

## Working directory resolution

The working directory is resolved in priority order:
1. Profile `workingDir` override (if set)
2. Thing-level `workingDir` default (if set)
3. The directory containing the script file

## Environment variables

Profile environment variables are merged on top of the standard Control Room variables (`CONTROLROOM_RUN_DIR`, `CONTROLROOM_RUN_ID`, etc.). This means your profile can set variables like `DEBUG=1` or `WANDB_PROJECT=myproject` and they will be available to the script alongside the run metadata.

## Creating profiles

1. Open the Thing detail view
2. Add a new profile with a name
3. Specify arguments (command-line flags)
4. Specify environment variables (key=value pairs)
5. Optionally set a working directory override
6. Save -- the profile is now available in the command palette

## When to use profiles

- **Smoke** -- quick validation with minimal data, fast feedback
- **Full** -- production-grade execution with all flags enabled
- **Debug** -- verbose logging, no caching, diagnostic environment variables
- **Custom** -- any combination specific to your workflow

## Schema migration

ThingConfig supports schema versioning. Legacy schema 1 configs (which lacked profiles) are automatically migrated to schema 2 on read, gaining a single Default profile.
