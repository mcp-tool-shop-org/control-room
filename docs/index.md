# Control Room

A local-first desktop app for managing and executing scripts, servers, and tasks with full observability.

## Key Features

- **Evidence-grade runs** — Every execution logged with stdout/stderr, exit codes, timing, and artifacts
- **Failure fingerprinting** — Recurring errors grouped and tracked across runs
- **Profiles** — Define preset argument/environment combinations per script
- **Command palette** — Keyboard-driven execution with fuzzy search
- **Timeline** — View all runs chronologically, filter by failure fingerprint
- **ZIP export** — Export any run as a complete evidence package

## Tech Stack

- .NET MAUI (Windows desktop)
- SQLite (WAL mode, local-first persistence)
- CommunityToolkit.Mvvm (source generators)

## Links

- [GitHub Repository](https://github.com/mcp-tool-shop-org/control-room)
- [MCP Tool Shop](https://github.com/mcp-tool-shop-org)
