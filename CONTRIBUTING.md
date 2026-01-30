# Contributing to Control Room

Thank you for considering contributing to Control Room! We welcome contributions from the community to help improve this local-first desktop app for managing and executing scripts with full observability.

## Code of Conduct

Before contributing, please read and adhere to our [Code of Conduct](CODE_OF_CONDUCT.md).

## Getting Started

### Prerequisites

- **.NET 10 SDK** - [Download](https://dotnet.microsoft.com/download)
- **Windows 10/11** (primary platform; MAUI also supports iOS, Android, macOS)
- **Visual Studio 2022** (recommended) or Visual Studio Code with C# Dev Kit

### Development Setup

```bash
# Clone the repository
git clone https://github.com/mcp-tool-shop/control-room.git
cd control-room

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run the application
dotnet run --project ControlRoom.App

# Run tests
dotnet test ControlRoom.Tests
```

## How to Contribute

### Reporting Bugs

1. **Search existing issues** - Check if the bug has already been reported
2. **Create a detailed issue** with:
   - Clear title describing the problem
   - Steps to reproduce
   - Expected vs. actual behavior
   - Screenshots/logs if applicable
   - Your environment (OS, .NET version)

### Suggesting Features

1. **Discuss first** - Open an issue to discuss the feature before investing time
2. **Describe the use case** - Explain the problem your feature solves
3. **Provide examples** - Show how the feature would be used

### Submitting Pull Requests

#### Before You Start

1. **Fork the repository** and create a feature branch:
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Follow the code style**:
   - Use 4-space indentation
   - Follow [C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
   - Use meaningful variable/method names
   - Add XML documentation for public APIs

3. **Organize code by layer**:
   - **Domain/** - Domain models (Thing, Run, RunEvent, etc.)
   - **Application/** - Use cases and business logic
   - **Infrastructure/** - SQLite storage, persistence
   - **App/** - MAUI UI components

#### Making Changes

1. **Create focused commits**:
   ```bash
   git commit -m "feat: add run filtering by tag

   - Add Tag property to Run entity
   - Implement tag-based filtering in RunQueries
   - Update UI timeline to show tag filters"
   ```

2. **Write tests** for your changes:
   - Add tests to `ControlRoom.Tests/`
   - Run locally: `dotnet test`
   - Aim for >= 80% line coverage on your code

3. **Add XML documentation** for public types/methods:
   ```csharp
   /// <summary>
   /// Represents a script or task to be executed.
   /// </summary>
   public record Thing(
       int Id,
       string Name,
       string ScriptPath)
   { }
   ```

#### Submitting

1. **Push to your fork**:
   ```bash
   git push origin feature/your-feature-name
   ```

2. **Open a Pull Request** with:
   - Clear title: "feat: describe your feature"
   - Description of changes and rationale
   - Reference to related issue: "Fixes #123"
   - Checklist (see below)

3. **PR Checklist**:
   - [ ] Tests pass locally (`dotnet test`)
   - [ ] No new compiler warnings
   - [ ] Changes follow [code style](#code-style)
   - [ ] XML documentation added
   - [ ] Changelog updated (if user-facing)
   - [ ] No security vulnerabilities introduced

## Development Workflow

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~RunQueriesTests"

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# Run in watch mode (requires dotnet-watch)
dotnet watch test
```

### Code Quality

```bash
# Format code
dotnet format

# Check for issues
dotnet build --no-restore

# Run static analysis
dotnet build -t:StyleCopAnalyzers
```

### Project Structure

```
ControlRoom/
├── ControlRoom.Domain/              # Domain models
│   ├── Thing.cs                     # Script/task definition
│   ├── Run.cs                       # Execution record
│   ├── RunEvent.cs                  # Execution event
│   └── ...
├── ControlRoom.Application/         # Use cases
│   ├── RunLocalScript.cs           # Run script use case
│   └── ...
├── ControlRoom.Infrastructure/      # Persistence
│   ├── Db/
│   ├── Queries/
│   └── ...
├── ControlRoom.App/                 # MAUI UI
│   ├── Views/
│   ├── ViewModels/
│   └── ...
└── ControlRoom.Tests/               # Tests
    ├── Fixtures/
    ├── Integration/
    └── Unit/
```

## Code Style

### Naming Conventions

- **Classes, Records**: `PascalCase` - `ThingConfig`, `RunSummary`
- **Methods, Properties**: `PascalCase` - `GetRunsByThing()`, `Fingerprint`
- **Private fields**: `_camelCase` - `_logger`, `_db`
- **Constants**: `UPPER_SNAKE_CASE` - `MAX_OUTPUT_SIZE`

### Patterns

1. **Use records for immutable data**:
   ```csharp
   public record Run(int Id, int ThingId, DateTime StartedAt, int? ExitCode);
   ```

2. **Use async/await properly**:
   ```csharp
   public async Task<List<Run>> GetRunsAsync(int thingId)
   {
       return await _db.GetRunsByThingAsync(thingId);
   }
   ```

3. **Validate inputs early**:
   ```csharp
   public void Execute(string scriptPath)
   {
       if (string.IsNullOrEmpty(scriptPath))
           throw new ArgumentException("Script path required");
   }
   ```

## Commit Message Guidelines

Use [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Types

- **feat** - New feature
- **fix** - Bug fix
- **docs** - Documentation
- **test** - Test additions/changes
- **refactor** - Code refactoring without feature changes
- **perf** - Performance improvements
- **chore** - Dependency updates, tooling

### Examples

```
feat(runs): add run filtering by exit code

- Add ExitCodeFilter to RunQueries
- Update UI timeline filter dropdown
- Add tests for filter combinations

Fixes #42
```

```
fix(storage): handle concurrent writes safely

Use WAL mode consistently and add transaction scope.

Fixes #89
```

## Documentation

### README Updates

Update the main [README.md](README.md) if your changes affect:
- Feature list
- Quick start instructions
- Project structure
- Tech stack

### Architecture Documentation

For significant architectural changes, add or update documentation in `docs/` (create if needed):
- Architecture decisions (ADRs)
- Database schema
- API contracts

## Submitting Improvements

### Documentation Improvements

We welcome improvements to documentation! Submit a PR with:
- Clear title: "docs: improve X documentation"
- Changes to `.md` files
- No other code changes unless necessary

### Performance Improvements

For performance work:
- Include benchmarks showing the improvement
- Document trade-offs
- Add performance tests if applicable

## Review Process

1. **Automated checks** run:
   - Tests pass
   - Code builds without warnings
   - Code coverage maintained

2. **Code review** by maintainers:
   - Architecture and design
   - Code quality and style
   - Test coverage
   - Performance impact

3. **Approval and merge** by maintainers

## Recognition

Contributors are recognized in:
- CHANGELOG.md - For significant features/fixes
- GitHub contributors page - Automatically updated

## Questions?

- **General questions**: Open a Discussion on GitHub
- **Issues**: Create an issue with detailed context
- **Security concerns**: See [SECURITY.md](SECURITY.md)

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

Thank you for contributing to Control Room! 🚀
