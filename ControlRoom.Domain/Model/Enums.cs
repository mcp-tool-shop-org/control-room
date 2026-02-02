namespace ControlRoom.Domain.Model;

public enum ThingKind
{
    LocalScript = 1
}

public enum RunStatus
{
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Canceled = 4
}

public enum EventKind
{
    RunStarted = 1,
    StdOut = 2,
    StdErr = 3,
    StatusChanged = 4,
    ArtifactAdded = 5,
    RunEnded = 6
}

/// <summary>
/// Condition type for runbook step execution
/// </summary>
public enum ConditionType
{
    /// <summary>Step always executes when dependencies complete</summary>
    Always = 1,
    /// <summary>Step executes only if all dependencies succeeded</summary>
    OnSuccess = 2,
    /// <summary>Step executes only if any dependency failed</summary>
    OnFailure = 3,
    /// <summary>Step executes based on expression evaluation</summary>
    Expression = 4
}

/// <summary>
/// Runbook execution status
/// </summary>
public enum RunbookExecutionStatus
{
    Pending = 1,
    Running = 2,
    Paused = 3,
    Succeeded = 4,
    Failed = 5,
    Canceled = 6,
    PartialSuccess = 7
}

/// <summary>
/// Individual step execution status
/// </summary>
public enum StepExecutionStatus
{
    Pending = 1,
    Waiting = 2,
    Running = 3,
    Succeeded = 4,
    Failed = 5,
    Skipped = 6,
    Canceled = 7
}

/// <summary>
/// Trigger type for runbook automation
/// </summary>
public enum TriggerType
{
    Manual = 1,
    Schedule = 2,
    Webhook = 3,
    FileWatch = 4
}
