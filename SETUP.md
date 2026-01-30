# Setup Guide

## Development Environment

### Prerequisites

- **Windows 10/11**
- **.NET 10 SDK** - Download from [microsoft.com/net](https://dotnet.microsoft.com/download)
- **Git** - Version control
- **Visual Studio 2022** or **VS Code** with C# extensions

### Optional Tools

- **Visual Studio 2022 Community** - Free IDE with built-in debugging
- **dotnet-watch** - Auto-rebuild on file changes
- **dotnet-format** - Code formatting

### Installation

```bash
# Clone repository
git clone https://github.com/mcp-tool-shop/control-room.git
cd control-room

# Restore packages
dotnet restore

# Verify setup
dotnet build
dotnet test
```

## Project Architecture

### Layer Structure

```
Domain Layer (ControlRoom.Domain/)
    ↓
Application Layer (ControlRoom.Application/)
    ↓
Infrastructure Layer (ControlRoom.Infrastructure/)
    ↓
UI Layer (ControlRoom.App/)
```

### Key Concepts

**Thing** - A script or task to manage
- Properties: Id, Name, ScriptPath, Description
- Can have multiple Profiles and Runs

**Profile** - A preset configuration for a Thing
- Properties: Name, Args, Environment variables
- Example: "Smoke" profile with limited data

**Run** - A single execution of a Thing with a specific Profile
- Properties: StartedAt, ExitCode, Duration, Status
- Contains Artifacts and Events

**RunEvent** - Individual execution event
- Types: Started, Completed, Error, Output
- Includes Timestamp and Description

**Artifact** - Output file from a Run
- Properties: Path, Size, Created date
- Can be exported with Run

### Data Flow

```
User Action (UI)
    ↓
View Model (MVVM binding)
    ↓
Application Use Case
    ↓
Domain Models
    ↓
Infrastructure Queries
    ↓
SQLite Database
```

## Development Workflow

### Running the Application

```bash
# Debug mode
dotnet run --project ControlRoom.App

# Release mode
dotnet run --project ControlRoom.App -c Release
```

### Testing

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "ClassName~RunQueriesTests"

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# Continuous testing
dotnet watch test
```

### Building for Distribution

```bash
# Build all platforms
dotnet build

# Build only Windows
dotnet build -c Release --no-restore

# Publish
dotnet publish -c Release --self-contained -r win-x64
```

## Database

### SQLite WAL Mode

The database uses **WAL (Write-Ahead Logging)** for safe concurrent access:

```sql
PRAGMA journal_mode=WAL;
```

**Files created**:
- `control-room.db` - Main database
- `control-room.db-wal` - Write-ahead log
- `control-room.db-shm` - Shared memory

### Schema

View schema in `ControlRoom.Infrastructure/Schema/` or check runtime:

```bash
dotnet run -- --debug-schema  # (if implemented)
```

### Adding Migrations

1. Update entity in Domain layer
2. Add migration (if using EF Core)
3. Update schema in Infrastructure
4. Run tests to verify

## Code Style

### Naming

- **Classes/Records**: `PascalCase`
- **Methods/Properties**: `PascalCase`
- **Private fields**: `_camelCase`
- **Constants**: `UPPER_CASE`
- **Async methods**: Suffix with `Async`

### Formatting

```csharp
// Records for immutable data
public record Run(int Id, int ThingId, DateTime StartedAt);

// Async methods
public async Task<Run> ExecuteAsync(string scriptPath)
{
    // Method body
}

// XML documentation
/// <summary>
/// Executes a script and tracks the run.
/// </summary>
public void Execute(string scriptPath) { }
```

## Common Tasks

### Adding a Feature

1. **Add domain model** in `ControlRoom.Domain/`
2. **Add use case** in `ControlRoom.Application/`
3. **Add infrastructure** (queries/commands) in `ControlRoom.Infrastructure/`
4. **Add UI components** in `ControlRoom.App/`
5. **Add tests** in `ControlRoom.Tests/`
6. **Update documentation**

### Adding Tests

```csharp
using Xunit;
using FluentAssertions;

public class RunQueriesTests
{
    [Fact]
    public async Task GetRunsByThing_ReturnsRunsInOrder()
    {
        // Arrange
        var db = new TestDatabaseFixture().Create();
        var thing = new Thing { Id = 1, Name = "Test" };
        
        // Act
        var runs = await RunQueries.GetByThingAsync(db, thing.Id);
        
        // Assert
        runs.Should().NotBeEmpty();
        runs.Should().BeInDescendingOrder(r => r.StartedAt);
    }
}
```

### Debugging

```bash
# Enable verbose logging
dotnet build --verbosity detailed

# Debug test
dotnet test --filter "TestName" -- --vs

# Attach debugger in VS Code
# Set breakpoints and press F5
```

## Troubleshooting

### Build Issues

```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build

# Check for .NET installation
dotnet --version
dotnet --info
```

### Test Failures

```bash
# Run with verbose output
dotnet test --verbosity normal

# Run single test
dotnet test --filter "TestClassName"

# Clear test results
rm -r TestResults/
dotnet test
```

### Database Issues

```bash
# Delete and recreate database
rm ControlRoom.db*
dotnet run  # Will create fresh DB

# Check WAL files
# If corrupted, delete .db-wal and .db-shm files
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for:
- Code style guidelines
- Commit message format
- Pull request process
- Testing requirements

## Performance Optimization

### Profiling

```bash
# Use built-in VS diagnostics
# Tools → Diagnostic Tools → CPU Usage

# Or use dotnet CLI
dotnet build -c Release
# Run with debugger sampling enabled
```

### Key Metrics

- **Run insertion**: < 100ms
- **Query runs (100 items)**: < 50ms
- **Export ZIP**: < 500ms

## Deployment

### Windows Desktop

1. Build release: `dotnet publish -c Release -r win-x64`
2. Create installer (optional): Use WiX or Inno Setup
3. Sign executable (recommended)
4. Create GitHub Release with assets

## Resources

- [.NET Documentation](https://learn.microsoft.com/dotnet/)
- [MAUI Guide](https://learn.microsoft.com/en-us/dotnet/maui/)
- [SQLite Documentation](https://www.sqlite.org/docs.html)
- [CommunityToolkit.MVVM](https://learn.microsoft.com/en-us/windows/communitytoolkit/mvvm/)

## Getting Help

- **Issues**: [GitHub Issues](https://github.com/mcp-tool-shop/control-room/issues)
- **Discussions**: [GitHub Discussions](https://github.com/mcp-tool-shop/control-room/discussions)
- **Security**: [SECURITY.md](SECURITY.md)
