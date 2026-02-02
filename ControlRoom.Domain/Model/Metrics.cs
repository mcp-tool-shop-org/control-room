namespace ControlRoom.Domain.Model;

/// <summary>
/// A single metric data point
/// </summary>
public sealed record MetricPoint(
    MetricId Id,
    string Name,
    MetricType Type,
    double Value,
    DateTimeOffset Timestamp,
    IReadOnlyDictionary<string, string> Tags
);

/// <summary>
/// Strongly-typed metric ID
/// </summary>
public readonly record struct MetricId(Guid Value)
{
    public static MetricId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

/// <summary>
/// Type of metric
/// </summary>
public enum MetricType
{
    Counter,    // Monotonically increasing value
    Gauge,      // Point-in-time value
    Histogram,  // Distribution of values
    Timer       // Duration measurement
}

/// <summary>
/// Aggregated metrics over a time window
/// </summary>
public sealed record MetricAggregate(
    string Name,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    TimeSpan Resolution,
    int Count,
    double Min,
    double Max,
    double Sum,
    double Avg,
    double P50,
    double P90,
    double P99,
    IReadOnlyDictionary<string, string> Tags
)
{
    public double StdDev => Count > 1
        ? Math.Sqrt(Variance)
        : 0;

    public double Variance { get; init; }
}

/// <summary>
/// Time series data for charting
/// </summary>
public sealed record MetricTimeSeries(
    string Name,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyList<TimeSeriesPoint> Points
);

/// <summary>
/// A point in a time series
/// </summary>
public sealed record TimeSeriesPoint(
    DateTimeOffset Timestamp,
    double Value
);

/// <summary>
/// Metric definitions for Control Room
/// </summary>
public static class MetricNames
{
    // Script execution metrics
    public const string ScriptDuration = "script.duration_ms";
    public const string ScriptSuccess = "script.success";
    public const string ScriptFailure = "script.failure";
    public const string ScriptOutputSize = "script.output_bytes";
    public const string ScriptExitCode = "script.exit_code";

    // Runbook metrics
    public const string RunbookDuration = "runbook.duration_ms";
    public const string RunbookSuccess = "runbook.success";
    public const string RunbookFailure = "runbook.failure";
    public const string RunbookStepsTotal = "runbook.steps_total";
    public const string RunbookStepsFailed = "runbook.steps_failed";
    public const string RunbookStepsSkipped = "runbook.steps_skipped";

    // System metrics
    public const string SystemCpuPercent = "system.cpu_percent";
    public const string SystemMemoryPercent = "system.memory_percent";
    public const string SystemDiskPercent = "system.disk_percent";
    public const string SystemProcessCount = "system.process_count";

    // Trigger metrics
    public const string TriggerFired = "trigger.fired";
    public const string TriggerLatency = "trigger.latency_ms";

    // Queue metrics
    public const string QueueDepth = "queue.depth";
    public const string QueueProcessingTime = "queue.processing_ms";
}

/// <summary>
/// Common metric tags
/// </summary>
public static class MetricTags
{
    public const string ThingId = "thing_id";
    public const string ThingName = "thing_name";
    public const string RunbookId = "runbook_id";
    public const string RunbookName = "runbook_name";
    public const string StepId = "step_id";
    public const string TriggerType = "trigger_type";
    public const string Status = "status";
    public const string ExitCode = "exit_code";
    public const string Host = "host";
}

/// <summary>
/// Alert definition based on metric conditions
/// </summary>
public sealed record AlertRule(
    AlertRuleId Id,
    string Name,
    string Description,
    string MetricName,
    AlertCondition Condition,
    double Threshold,
    TimeSpan EvaluationWindow,
    TimeSpan CooldownPeriod,
    AlertSeverity Severity,
    bool IsEnabled,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyList<AlertAction> Actions
);

/// <summary>
/// Strongly-typed alert rule ID
/// </summary>
public readonly record struct AlertRuleId(Guid Value)
{
    public static AlertRuleId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

/// <summary>
/// Alert condition operators
/// </summary>
public enum AlertCondition
{
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Equal,
    NotEqual,
    AbsoluteChange,      // Value changed by more than threshold
    PercentChange,       // Value changed by more than threshold %
    Anomaly              // Statistical anomaly detection
}

/// <summary>
/// Alert severity levels
/// </summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Action to take when alert fires
/// </summary>
public sealed record AlertAction(
    AlertActionType Type,
    IReadOnlyDictionary<string, string> Config
);

/// <summary>
/// Types of alert actions
/// </summary>
public enum AlertActionType
{
    Notification,    // Show in-app notification
    Email,           // Send email
    Webhook,         // POST to webhook URL
    RunRunbook,      // Execute a runbook (self-healing)
    Script           // Run a custom script
}

/// <summary>
/// Fired alert instance
/// </summary>
public sealed record Alert(
    AlertId Id,
    AlertRuleId RuleId,
    string RuleName,
    AlertSeverity Severity,
    string Message,
    double CurrentValue,
    double Threshold,
    DateTimeOffset FiredAt,
    DateTimeOffset? ResolvedAt,
    AlertStatus Status,
    IReadOnlyDictionary<string, string> Tags
)
{
    public bool IsResolved => ResolvedAt.HasValue;
    public TimeSpan? Duration => ResolvedAt.HasValue
        ? ResolvedAt.Value - FiredAt
        : DateTimeOffset.UtcNow - FiredAt;
}

/// <summary>
/// Strongly-typed alert ID
/// </summary>
public readonly record struct AlertId(Guid Value)
{
    public static AlertId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

/// <summary>
/// Alert status
/// </summary>
public enum AlertStatus
{
    Firing,
    Acknowledged,
    Resolved,
    Suppressed
}

/// <summary>
/// Health check definition
/// </summary>
public sealed record HealthCheck(
    HealthCheckId Id,
    string Name,
    string Description,
    HealthCheckType Type,
    IReadOnlyDictionary<string, string> Config,
    TimeSpan Interval,
    TimeSpan Timeout,
    bool IsEnabled
);

/// <summary>
/// Strongly-typed health check ID
/// </summary>
public readonly record struct HealthCheckId(Guid Value)
{
    public static HealthCheckId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

/// <summary>
/// Types of health checks
/// </summary>
public enum HealthCheckType
{
    Http,           // HTTP endpoint check
    Tcp,            // TCP port check
    Dns,            // DNS resolution check
    Ping,           // ICMP ping
    Script,         // Custom script check
    Database,       // Database connection check
    Service         // Windows service check
}

/// <summary>
/// Result of a health check
/// </summary>
public sealed record HealthCheckResult(
    HealthCheckId CheckId,
    string CheckName,
    HealthStatus Status,
    DateTimeOffset CheckedAt,
    TimeSpan ResponseTime,
    string? Message,
    IReadOnlyDictionary<string, object>? Details
);

/// <summary>
/// Health status
/// </summary>
public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy,
    Unknown
}

/// <summary>
/// Self-healing action definition
/// </summary>
public sealed record SelfHealingRule(
    SelfHealingRuleId Id,
    string Name,
    string Description,
    string TriggerCondition,  // Expression to evaluate (e.g., "alert.severity == Critical && alert.metric == 'service.health'")
    RunbookId RemediationRunbook,
    int MaxExecutionsPerHour,
    TimeSpan CooldownPeriod,
    bool RequiresApproval,
    bool IsEnabled
);

/// <summary>
/// Strongly-typed self-healing rule ID
/// </summary>
public readonly record struct SelfHealingRuleId(Guid Value)
{
    public static SelfHealingRuleId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

/// <summary>
/// Record of a self-healing action
/// </summary>
public sealed record SelfHealingExecution(
    SelfHealingExecutionId Id,
    SelfHealingRuleId RuleId,
    AlertId? TriggeringAlert,
    RunbookExecutionId? RemediationExecution,
    SelfHealingStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? Result
);

/// <summary>
/// Strongly-typed self-healing execution ID
/// </summary>
public readonly record struct SelfHealingExecutionId(Guid Value)
{
    public static SelfHealingExecutionId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

/// <summary>
/// Status of self-healing execution
/// </summary>
public enum SelfHealingStatus
{
    Pending,
    AwaitingApproval,
    Running,
    Succeeded,
    Failed,
    Skipped      // Skipped due to cooldown or rate limit
}
