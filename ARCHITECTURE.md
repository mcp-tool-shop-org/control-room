# Architecture Decision Records

This document tracks architectural decisions made in the Control Room project.

## ADR-001: Local-First SQLite Storage

**Status**: Adopted  
**Date**: 2024-01-01

### Context

Control Room manages execution runs and artifacts for local scripts. We needed persistent storage that:
- Works offline
- Requires no server
- Supports concurrent access
- Minimizes dependencies

### Decision

Use **SQLite with WAL (Write-Ahead Logging)** mode for local-first storage.

### Rationale

- ✅ Zero-config, file-based database
- ✅ Built-in with .NET
- ✅ WAL mode supports safe concurrent reads/writes
- ✅ No external dependencies or services required
- ✅ Easy to backup/transfer (single file)

### Consequences

- Database is local-only (no cloud sync)
- Multiple instances can't share data
- Users must manage backups manually
- Performance limited to disk I/O

### Alternatives Considered

- **EF Core + SQL Server**: Too heavyweight for local usage
- **File-based JSON**: No concurrent access safety
- **RocksDB**: Adds native dependency complexity

---

## ADR-002: Domain-Driven Design with Clean Architecture

**Status**: Adopted  
**Date**: 2024-01-01

### Context

The application needs to evolve as features are added. We need clear boundaries between:
- Business logic (Domain)
- Use cases (Application)
- Infrastructure concerns (Database)
- User interface (MAUI)

### Decision

Implement **Clean Architecture** with 4 layers:
1. **Domain** - Core business models (Thing, Run, RunEvent)
2. **Application** - Use cases (RunLocalScript, ListRuns)
3. **Infrastructure** - Persistence (SQLite queries)
4. **Presentation** - MAUI UI (Views, ViewModels)

### Rationale

- ✅ Clear separation of concerns
- ✅ Business logic independent of UI/DB
- ✅ Easy to test core functionality
- ✅ Changes to UI don't affect business logic
- ✅ Follows established patterns

### Consequences

- More classes/files than monolithic approach
- Requires discipline in maintaining boundaries
- Dependency injection setup overhead
- Learning curve for new contributors

### Code Organization

```
Domain/              # No external dependencies
├── Thing.cs
├── Run.cs
└── ...

Application/         # Depends on Domain only
├── RunLocalScript.cs
└── ...

Infrastructure/      # Depends on Domain, implements interfaces
├── Queries/
└── Persistence/

App/                 # MAUI UI, depends on all others
├── Views/
└── ViewModels/
```

---

## ADR-003: Records for Immutable Value Objects

**Status**: Adopted  
**Date**: 2024-01-01

### Context

Domain models like `Run`, `Thing`, and `RunEvent` are primarily data containers. We need to:
- Ensure immutability
- Prevent accidental mutations
- Simplify equality comparisons
- Support pattern matching

### Decision

Use **C# records** for all domain models instead of traditional classes with properties.

### Example

```csharp
// ✅ Good
public record Run(int Id, int ThingId, DateTime StartedAt, int? ExitCode);

// ❌ Avoid
public class Run
{
    public int Id { get; set; }
    public int ThingId { get; set; }
    // ... etc
}
```

### Rationale

- ✅ Immutability by design
- ✅ Built-in value-based equality
- ✅ Less boilerplate code
- ✅ Pattern matching support
- ✅ Reference semantics when needed (with `class`)

### Consequences

- C# 9+ only (but .NET 10 uses C# 13)
- Records are verbose with many properties
- Can't use `init` for selective immutability (less flexible)

---

## ADR-004: MVVM with CommunityToolkit

**Status**: Adopted  
**Date**: 2024-01-01

### Context

MAUI requires data binding between Views and business logic. We need:
- Simple property binding
- Command binding for user actions
- MVVM framework to reduce boilerplate

### Decision

Use **CommunityToolkit.MVVM** from Microsoft for ViewModels with:
- `ObservableObject` base class for `INotifyPropertyChanged`
- `RelayCommand` for command binding
- Dependency injection via constructor

### Example

```csharp
public partial class TimelineViewModel : ObservableObject
{
    [ObservableProperty]
    private List<Run> runs = [];
    
    [RelayCommand]
    private async Task LoadRuns()
    {
        Runs = await _queries.GetRunsAsync();
    }
}
```

### Rationale

- ✅ Official Microsoft toolkit
- ✅ Source generators reduce boilerplate
- ✅ Modern async/await patterns
- ✅ Minimal external dependencies

---

## ADR-005: Async/Await for Database Operations

**Status**: Adopted  
**Date**: 2024-01-01

### Context

Database queries can be slow. We must avoid:
- Blocking the UI thread
- Poor responsiveness
- "Application Not Responding" errors

### Decision

All I/O operations use `async`/`await`:
- Database queries return `Task<T>`
- ViewModels use `async` commands
- UI remains responsive

### Example

```csharp
public async Task<List<Run>> GetRunsAsync(int thingId)
{
    return await _db.QueryAsync(
        "SELECT * FROM runs WHERE thing_id = @thingId ORDER BY started_at DESC",
        new { thingId }
    );
}

[RelayCommand]
private async Task LoadRuns()
{
    Runs = await _queries.GetRunsAsync(_selectedThingId);
}
```

### Rationale

- ✅ Responsive UI
- ✅ Better resource utilization
- ✅ Scalable architecture
- ✅ Native to .NET platform

---

## ADR-006: WAL Mode for Concurrent Access

**Status**: Adopted  
**Date**: 2024-01-01

### Context

Multiple components need to read from the database while writes occur:
- UI reads runs while background process logs new events
- Export ZIP while user navigates timeline
- Multiple queries executing simultaneously

### Decision

Use SQLite **WAL (Write-Ahead Logging)** mode:
```sql
PRAGMA journal_mode=WAL;
```

### Rationale

- ✅ Readers don't block writers
- ✅ Writers don't block readers
- ✅ Better concurrency than default journal
- ✅ Minimal performance overhead

### Consequences

- Creates `-wal` and `-shm` files alongside database
- Requires cleanup during backup
- Not compatible with network shares
- Must stay on local machine

---

## ADR-007: Failure Fingerprinting Strategy

**Status**: Adopted  
**Date**: 2024-01-01

### Context

Similar errors should be grouped to identify recurring issues. We need:
- Deterministic error categorization
- Grouping by error pattern
- Query runs by error signature

### Decision

Compute `Fingerprint` from error:
```csharp
public string ComputeFingerprint(string stderr)
{
    // Extract error message (first line, remove timestamps)
    var errorLine = stderr.Split('\n').FirstOrDefault() ?? "";
    return SHA256.Hash(errorLine);
}
```

### Rationale

- ✅ Consistent error grouping
- ✅ User-facing error detection
- ✅ Tracks recurring problems
- ✅ Simple to understand and maintain

---

## Related Topics

- [SETUP.md](SETUP.md) - Development environment setup
- [CONTRIBUTING.md](CONTRIBUTING.md) - Contributing guidelines
- Database schema in infrastructure layer
