# Control Room - Test Setup Guide

## Project Structure Overview

### Solution Layout
- **ControlRoom.sln** - Main solution file (Visual Studio 17, .NET 10.0)
- **ControlRoom.Domain** - Core domain models and entities
  - `Model/` - Contains record types: `Run`, `Thing`, `ThingConfig`, `Artifact`, `RunEvent`, `RunSummary`, Enums, Ids
  - Pure business logic, no dependencies
  
- **ControlRoom.Application** - Use cases and business orchestration
  - `UseCases/` - Contains `RunLocalScript.cs` (main orchestration logic)
  - References: Domain + Infrastructure
  
- **ControlRoom.Infrastructure** - Data access and process execution
  - `Process/` - Script execution: `ScriptRunner.cs`, `IScriptRunner` interface
  - `Storage/` - SQLite database layer: `Db.cs`, `Migrator.cs`, `Schema.sql`, `AppSettings.cs`, Query builders
  - References: Domain
  
- **ControlRoom.App** - Desktop UI application

### Existing Test Infrastructure
- `test-scripts/` - Simple PowerShell test scripts
  - `hello.ps1` - Basic script
  - `hello-artifacts.ps1` - Script that produces artifacts

### Technology Stack
- **.NET 10.0** (with Windows 10.0.19041.0 variant)
- **SQLite** via `Microsoft.Data.Sqlite` 10.0.2
- **C# 13** with nullable reference types and implicit usings enabled
- No existing test projects (xUnit/NUnit/MSTest not yet added)

## Key Components to Test

### 1. Domain Models (ControlRoom.Domain)
- Record types with immutable data structures
- Enum types for statuses and kinds
- ID types (RunId, ThingId)
- **Best for:** Unit tests (no dependencies)
- **Framework:** xUnit or NUnit

### 2. Use Cases (ControlRoom.Application)
- `RunLocalScript` - Complex orchestration with:
  - Script execution coordination
  - Profile resolution from JSON config
  - Exit code tracking
  - Summary generation
- **Best for:** Integration/Unit tests with mocks
- **Needs:** Mock `Db`, Mock `IScriptRunner`

### 3. Infrastructure (ControlRoom.Infrastructure)

#### ScriptRunner
- Process execution using `System.Diagnostics`
- Cross-platform launcher resolution (.ps1, .py, .sh, etc.)
- Command line building and argument escaping
- Output streaming via callback
- **Best for:** Integration tests (real process execution) or unit tests (mocked System.Diagnostics)

#### Database Layer (Db)
- SQLite connection management with WAL mode
- Query builders for data access
- Schema migrations
- **Best for:** Integration tests with in-memory or temp SQLite databases

## Recommended Test Project Structure

```
ControlRoom.Tests/
├── ControlRoom.Tests.csproj
├── Unit/
│   ├── Domain/
│   │   ├── ModelTests.cs (Run, Thing, ThingConfig, etc.)
│   │   └── EnumTests.cs (Status, Kind validation)
│   ├── Application/
│   │   └── RunLocalScriptTests.cs (mocked infrastructure)
│   └── Infrastructure/
│       ├── ScriptRunnerTests.cs
│       └── DbTests.cs
├── Integration/
│   ├── RunLocalScriptIntegrationTests.cs (real process + DB)
│   ├── DatabaseIntegrationTests.cs
│   └── ProcessExecutionTests.cs
├── Fixtures/
│   ├── TestDbFixture.cs (in-memory SQLite)
│   ├── MockScriptRunnerFixture.cs
│   └── TestDataBuilder.cs
└── Resources/
    └── test-scripts/ (symlink or copy of test-scripts/)
```

## Test Frameworks to Use

### Recommended Stack
- **xUnit** - Modern, fact-based testing framework
  - `xunit` package
  - `xunit.runner.visualstudio` for Visual Studio integration
  
- **Moq** - Mocking library for `IScriptRunner`, `Db` abstractions
  - `Moq` package
  
- **FluentAssertions** - Readable assertion syntax
  - `FluentAssertions` package
  
- **xunit.runner.console** - CLI test runner (optional)

### Alternative
- **NUnit** + **Moq** (more familiar to some teams)
- **MSTest** (built-in to Visual Studio)

## Key Testing Challenges & Solutions

### Challenge 1: Testing Process Execution
**Issue:** `ScriptRunner` actually launches processes
- **Solution 1:** Mock `IScriptRunner` in application tests
- **Solution 2:** Use integration tests with simple test scripts (already exist in `test-scripts/`)
- **Solution 3:** Create mock implementation of `IScriptRunner` that simulates execution

### Challenge 2: Database Testing
**Issue:** `Db` uses real SQLite files
- **Solution 1:** Use in-memory SQLite (`:memory:` connection string)
- **Solution 2:** Create temporary test databases in `Path.GetTempPath()`
- **Solution 3:** Implement `IDb` interface for better testability

### Challenge 3: Testing Async/Cancellation
**Issue:** `RunLocalScript` and `ScriptRunner` use `CancellationToken`
- **Solution:** Use `CancellationTokenSource` in tests to simulate cancellation scenarios

## Required .csproj Dependencies

```xml
<ItemGroup>
  <ProjectReference Include="..\ControlRoom.Domain\ControlRoom.Domain.csproj" />
  <ProjectReference Include="..\ControlRoom.Application\ControlRoom.Application.csproj" />
  <ProjectReference Include="..\ControlRoom.Infrastructure\ControlRoom.Infrastructure.csproj" />
</ItemGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.10.0" />
  <PackageReference Include="xunit" Version="2.7.0" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.7.0" />
  <PackageReference Include="Moq" Version="4.20.70" />
  <PackageReference Include="FluentAssertions" Version="6.12.0" />
</ItemGroup>
```

## Getting Started

1. **Create test project:**
   ```bash
   dotnet new xunit -n ControlRoom.Tests
   dotnet sln add ControlRoom.Tests/ControlRoom.Tests.csproj
   ```

2. **Add project references to ControlRoom.Tests.csproj**

3. **Create fixture classes for common setup**

4. **Start with domain model tests** (easiest, no dependencies)

5. **Move to infrastructure tests** (more complex, external dependencies)

6. **Add integration tests** (full workflow tests)

## Sample Test Class Template

```csharp
using Xunit;
using FluentAssertions;
using Moq;
using ControlRoom.Domain.Model;
using ControlRoom.Application.UseCases;
using ControlRoom.Infrastructure.Process;
using ControlRoom.Infrastructure.Storage;

namespace ControlRoom.Tests.Unit.Application;

public class RunLocalScriptTests
{
    private readonly Mock<Db> _mockDb;
    private readonly Mock<IScriptRunner> _mockRunner;
    private readonly RunLocalScript _useCase;

    public RunLocalScriptTests()
    {
        _mockDb = new Mock<Db>();
        _mockRunner = new Mock<IScriptRunner>();
        _useCase = new RunLocalScript(_mockDb.Object, _mockRunner.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidThing_ReturnsRunId()
    {
        // Arrange
        var thing = new Thing(
            Id: ThingId.New(),
            Name: "test-script",
            Kind: ThingKind.Script,
            ConfigJson: "{}",
            CreatedAt: DateTimeOffset.UtcNow
        );

        // Act
        var runId = await _useCase.ExecuteAsync(thing, "", CancellationToken.None);

        // Assert
        runId.Should().NotBeNull();
    }
}
```

## Notes

- Consider creating abstractions around `Db` for better testability (currently direct dependency)
- Existing `test-scripts/` are valuable for integration testing
- `.NET 10.0` is bleeding edge; ensure test framework supports it
- SQLite provides good in-memory testing capabilities
