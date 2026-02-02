using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControlRoom.Domain.Model;

/// <summary>
/// A runbook is a multi-step workflow that orchestrates multiple scripts (Things).
/// Steps form a DAG (Directed Acyclic Graph) with dependencies and conditions.
/// </summary>
public sealed record Runbook(
    RunbookId Id,
    string Name,
    string Description,
    IReadOnlyList<RunbookStep> Steps,
    RunbookTrigger? Trigger,
    bool IsEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
)
{
    /// <summary>
    /// Validate the runbook configuration
    /// </summary>
    public RunbookValidationResult Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Runbook name is required");

        if (Steps.Count == 0)
            errors.Add("Runbook must have at least one step");

        // Check for duplicate step IDs
        var stepIds = Steps.Select(s => s.StepId).ToList();
        var duplicates = stepIds.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        foreach (var dup in duplicates)
            errors.Add($"Duplicate step ID: {dup}");

        // Check for invalid dependencies (referencing non-existent steps)
        var stepIdSet = new HashSet<string>(stepIds);
        foreach (var step in Steps)
        {
            foreach (var dep in step.DependsOn)
            {
                if (!stepIdSet.Contains(dep))
                    errors.Add($"Step '{step.StepId}' depends on non-existent step '{dep}'");
            }
        }

        // Check for cycles (only if no duplicates - cycle detection requires unique IDs)
        if (duplicates.Count == 0 && HasCycle())
            errors.Add("Runbook contains a dependency cycle");

        return new RunbookValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Detect cycles in the step dependency graph using DFS
    /// </summary>
    public bool HasCycle()
    {
        var adjacency = Steps.ToDictionary(s => s.StepId, s => s.DependsOn.ToList());
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var step in Steps)
        {
            if (HasCycleDfs(step.StepId, adjacency, visited, recursionStack))
                return true;
        }

        return false;
    }

    private static bool HasCycleDfs(
        string stepId,
        Dictionary<string, List<string>> adjacency,
        HashSet<string> visited,
        HashSet<string> recursionStack)
    {
        if (recursionStack.Contains(stepId))
            return true;

        if (visited.Contains(stepId))
            return false;

        visited.Add(stepId);
        recursionStack.Add(stepId);

        if (adjacency.TryGetValue(stepId, out var deps))
        {
            foreach (var dep in deps)
            {
                if (adjacency.ContainsKey(dep) && HasCycleDfs(dep, adjacency, visited, recursionStack))
                    return true;
            }
        }

        recursionStack.Remove(stepId);
        return false;
    }

    /// <summary>
    /// Get steps in topological order (dependencies first)
    /// </summary>
    public IReadOnlyList<RunbookStep> GetTopologicalOrder()
    {
        var result = new List<RunbookStep>();
        var visited = new HashSet<string>();
        var stepMap = Steps.ToDictionary(s => s.StepId);

        void Visit(string stepId)
        {
            if (visited.Contains(stepId)) return;
            visited.Add(stepId);

            if (stepMap.TryGetValue(stepId, out var step))
            {
                foreach (var dep in step.DependsOn)
                    Visit(dep);
                result.Add(step);
            }
        }

        foreach (var step in Steps)
            Visit(step.StepId);

        return result;
    }

    /// <summary>
    /// Get steps that have no dependencies (entry points)
    /// </summary>
    public IReadOnlyList<RunbookStep> GetEntryPoints()
    {
        return Steps.Where(s => s.DependsOn.Count == 0).ToList();
    }

    /// <summary>
    /// Get steps that depend on a specific step
    /// </summary>
    public IReadOnlyList<RunbookStep> GetDependents(string stepId)
    {
        return Steps.Where(s => s.DependsOn.Contains(stepId)).ToList();
    }
}

/// <summary>
/// Result of runbook validation
/// </summary>
public sealed record RunbookValidationResult(bool IsValid, IReadOnlyList<string> Errors);

/// <summary>
/// A single step in a runbook workflow
/// </summary>
public sealed record RunbookStep(
    string StepId,
    string Name,
    ThingId ThingId,
    string ProfileId,
    StepCondition Condition,
    IReadOnlyList<string> DependsOn,
    RetryPolicy? Retry,
    TimeSpan? Timeout,
    string? ArgumentsOverride
)
{
    /// <summary>
    /// Check if this step should execute based on dependency results
    /// </summary>
    public bool ShouldExecute(IReadOnlyDictionary<string, StepExecutionStatus> dependencyResults)
    {
        if (DependsOn.Count == 0)
            return true;

        return Condition.Type switch
        {
            ConditionType.Always => DependsOn.All(d =>
                dependencyResults.TryGetValue(d, out var status) &&
                (status == StepExecutionStatus.Succeeded || status == StepExecutionStatus.Failed)),

            ConditionType.OnSuccess => DependsOn.All(d =>
                dependencyResults.TryGetValue(d, out var status) &&
                status == StepExecutionStatus.Succeeded),

            ConditionType.OnFailure => DependsOn.Any(d =>
                dependencyResults.TryGetValue(d, out var status) &&
                status == StepExecutionStatus.Failed),

            ConditionType.Expression => EvaluateExpression(Condition.Expression, dependencyResults),

            _ => false
        };
    }

    /// <summary>
    /// Evaluate a simple expression against dependency results.
    /// Supports: step.succeeded, step.failed, AND, OR, NOT, parentheses
    /// </summary>
    private static bool EvaluateExpression(
        string? expression,
        IReadOnlyDictionary<string, StepExecutionStatus> results)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return true;

        // Simple expression parser for conditions like:
        // "step1.succeeded AND step2.succeeded"
        // "step1.failed OR step2.failed"
        // "NOT step1.failed"
        var tokens = Tokenize(expression);
        return EvaluateTokens(tokens, results);
    }

    private static List<string> Tokenize(string expression)
    {
        var tokens = new List<string>();
        var current = "";

        foreach (var c in expression)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!string.IsNullOrEmpty(current))
                {
                    tokens.Add(current);
                    current = "";
                }
            }
            else if (c == '(' || c == ')')
            {
                if (!string.IsNullOrEmpty(current))
                {
                    tokens.Add(current);
                    current = "";
                }
                tokens.Add(c.ToString());
            }
            else
            {
                current += c;
            }
        }

        if (!string.IsNullOrEmpty(current))
            tokens.Add(current);

        return tokens;
    }

    private static bool EvaluateTokens(
        List<string> tokens,
        IReadOnlyDictionary<string, StepExecutionStatus> results)
    {
        if (tokens.Count == 0)
            return true;

        // Handle NOT
        if (tokens.Count >= 2 && tokens[0].Equals("NOT", StringComparison.OrdinalIgnoreCase))
        {
            return !EvaluateTokens(tokens.Skip(1).ToList(), results);
        }

        // Handle parentheses
        if (tokens.Count >= 3 && tokens[0] == "(")
        {
            var depth = 0;
            var closeIndex = -1;
            for (var i = 0; i < tokens.Count; i++)
            {
                if (tokens[i] == "(") depth++;
                else if (tokens[i] == ")")
                {
                    depth--;
                    if (depth == 0)
                    {
                        closeIndex = i;
                        break;
                    }
                }
            }

            if (closeIndex > 0)
            {
                var inner = tokens.Skip(1).Take(closeIndex - 1).ToList();
                var innerResult = EvaluateTokens(inner, results);

                if (closeIndex == tokens.Count - 1)
                    return innerResult;

                var remaining = tokens.Skip(closeIndex + 1).ToList();
                if (remaining.Count >= 2)
                {
                    var op = remaining[0];
                    var rest = remaining.Skip(1).ToList();

                    if (op.Equals("AND", StringComparison.OrdinalIgnoreCase))
                        return innerResult && EvaluateTokens(rest, results);
                    if (op.Equals("OR", StringComparison.OrdinalIgnoreCase))
                        return innerResult || EvaluateTokens(rest, results);
                }
            }
        }

        // Handle AND/OR
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Equals("AND", StringComparison.OrdinalIgnoreCase))
            {
                var left = EvaluateTokens(tokens.Take(i).ToList(), results);
                var right = EvaluateTokens(tokens.Skip(i + 1).ToList(), results);
                return left && right;
            }

            if (tokens[i].Equals("OR", StringComparison.OrdinalIgnoreCase))
            {
                var left = EvaluateTokens(tokens.Take(i).ToList(), results);
                var right = EvaluateTokens(tokens.Skip(i + 1).ToList(), results);
                return left || right;
            }
        }

        // Handle simple condition: step.succeeded or step.failed
        if (tokens.Count == 1)
        {
            var condition = tokens[0];
            var parts = condition.Split('.');
            if (parts.Length == 2)
            {
                var stepId = parts[0];
                var state = parts[1].ToLowerInvariant();

                if (results.TryGetValue(stepId, out var status))
                {
                    return state switch
                    {
                        "succeeded" => status == StepExecutionStatus.Succeeded,
                        "failed" => status == StepExecutionStatus.Failed,
                        "skipped" => status == StepExecutionStatus.Skipped,
                        "completed" => status is StepExecutionStatus.Succeeded or StepExecutionStatus.Failed,
                        _ => false
                    };
                }
            }
        }

        return false;
    }
}

/// <summary>
/// Condition for step execution
/// </summary>
public sealed record StepCondition(
    ConditionType Type,
    string? Expression = null
)
{
    public static StepCondition Always => new(ConditionType.Always);
    public static StepCondition OnSuccess => new(ConditionType.OnSuccess);
    public static StepCondition OnFailure => new(ConditionType.OnFailure);
    public static StepCondition FromExpression(string expr) => new(ConditionType.Expression, expr);
}

/// <summary>
/// Retry policy for failed steps
/// </summary>
public sealed record RetryPolicy(
    int MaxAttempts,
    TimeSpan InitialDelay,
    double BackoffMultiplier,
    TimeSpan MaxDelay
)
{
    public static RetryPolicy Default => new(3, TimeSpan.FromSeconds(5), 2.0, TimeSpan.FromMinutes(5));

    /// <summary>
    /// Calculate delay for a specific attempt (0-based)
    /// </summary>
    public TimeSpan GetDelay(int attempt)
    {
        if (attempt <= 0) return TimeSpan.Zero;

        var delay = InitialDelay.TotalMilliseconds * Math.Pow(BackoffMultiplier, attempt - 1);
        var capped = Math.Min(delay, MaxDelay.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(capped);
    }
}

/// <summary>
/// Base type for runbook triggers
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ManualTrigger), "manual")]
[JsonDerivedType(typeof(ScheduleTrigger), "schedule")]
[JsonDerivedType(typeof(WebhookTrigger), "webhook")]
[JsonDerivedType(typeof(FileWatchTrigger), "filewatch")]
public abstract record RunbookTrigger
{
    public abstract TriggerType TriggerType { get; }
}

/// <summary>
/// Manual trigger - runbook is started by user action
/// </summary>
public sealed record ManualTrigger() : RunbookTrigger
{
    public override TriggerType TriggerType => TriggerType.Manual;
}

/// <summary>
/// Schedule trigger - runbook runs on a cron schedule
/// </summary>
public sealed record ScheduleTrigger(
    string CronExpression,
    string? TimeZoneId = null
) : RunbookTrigger
{
    public override TriggerType TriggerType => TriggerType.Schedule;

    /// <summary>
    /// Get the next scheduled run time (simplified - real implementation would use NCrontab)
    /// </summary>
    public DateTimeOffset? GetNextRun(DateTimeOffset after)
    {
        // Placeholder - real implementation would parse cron expression
        // For now, return null to indicate "not implemented"
        return null;
    }
}

/// <summary>
/// Webhook trigger - runbook is triggered by HTTP POST
/// </summary>
public sealed record WebhookTrigger(
    string Secret,
    string? AllowedIpRange = null
) : RunbookTrigger
{
    public override TriggerType TriggerType => TriggerType.Webhook;

    /// <summary>
    /// Validate HMAC signature for webhook security
    /// </summary>
    public bool ValidateSignature(string payload, string signature)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes(Secret));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
        var expected = Convert.ToHexStringLower(hash);
        return string.Equals(signature, expected, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(signature, $"sha256={expected}", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// File watch trigger - runbook runs when files change
/// </summary>
public sealed record FileWatchTrigger(
    string Path,
    string Pattern,
    bool IncludeSubdirectories = false,
    TimeSpan? Debounce = null
) : RunbookTrigger
{
    public override TriggerType TriggerType => TriggerType.FileWatch;
}

/// <summary>
/// Represents a single execution of a runbook
/// </summary>
public sealed record RunbookExecution(
    RunbookExecutionId Id,
    RunbookId RunbookId,
    RunbookExecutionStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    IReadOnlyList<StepExecution> StepExecutions,
    string? TriggerInfo,
    string? ErrorMessage
)
{
    /// <summary>
    /// Get the overall duration of the execution
    /// </summary>
    public TimeSpan? Duration => EndedAt.HasValue ? EndedAt.Value - StartedAt : null;

    /// <summary>
    /// Get count of steps by status
    /// </summary>
    public IReadOnlyDictionary<StepExecutionStatus, int> GetStatusCounts()
    {
        return StepExecutions
            .GroupBy(s => s.Status)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Check if execution is complete
    /// </summary>
    public bool IsComplete => Status is RunbookExecutionStatus.Succeeded
        or RunbookExecutionStatus.Failed
        or RunbookExecutionStatus.Canceled
        or RunbookExecutionStatus.PartialSuccess;
}

/// <summary>
/// Represents execution of a single step within a runbook execution
/// </summary>
public sealed record StepExecution(
    string StepId,
    string StepName,
    RunId? RunId,
    StepExecutionStatus Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    int Attempt,
    string? ErrorMessage,
    string? Output
)
{
    public TimeSpan? Duration => (StartedAt.HasValue && EndedAt.HasValue)
        ? EndedAt.Value - StartedAt.Value
        : null;
}

/// <summary>
/// Configuration for creating/updating a runbook (used in serialization)
/// </summary>
public sealed class RunbookConfig
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<RunbookStepConfig> Steps { get; set; } = [];
    public RunbookTrigger? Trigger { get; set; }
    public bool IsEnabled { get; set; } = true;

    public static RunbookConfig Parse(string json)
    {
        return JsonSerializer.Deserialize<RunbookConfig>(json, JsonOptions) ?? new();
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

/// <summary>
/// Step configuration for serialization
/// </summary>
public sealed class RunbookStepConfig
{
    public string StepId { get; set; } = "";
    public string Name { get; set; } = "";
    public string ThingId { get; set; } = "";
    public string ProfileId { get; set; } = "default";
    public ConditionType ConditionType { get; set; } = ConditionType.OnSuccess;
    public string? ConditionExpression { get; set; }
    public List<string> DependsOn { get; set; } = [];
    public RetryPolicyConfig? Retry { get; set; }
    public int? TimeoutSeconds { get; set; }
    public string? ArgumentsOverride { get; set; }

    public RunbookStep ToStep()
    {
        return new RunbookStep(
            StepId,
            Name,
            new ThingId(Guid.Parse(ThingId)),
            ProfileId,
            new StepCondition(ConditionType, ConditionExpression),
            DependsOn,
            Retry?.ToPolicy(),
            TimeoutSeconds.HasValue ? TimeSpan.FromSeconds(TimeoutSeconds.Value) : null,
            ArgumentsOverride
        );
    }
}

/// <summary>
/// Retry policy configuration for serialization
/// </summary>
public sealed class RetryPolicyConfig
{
    public int MaxAttempts { get; set; } = 3;
    public int InitialDelaySeconds { get; set; } = 5;
    public double BackoffMultiplier { get; set; } = 2.0;
    public int MaxDelaySeconds { get; set; } = 300;

    public RetryPolicy ToPolicy()
    {
        return new RetryPolicy(
            MaxAttempts,
            TimeSpan.FromSeconds(InitialDelaySeconds),
            BackoffMultiplier,
            TimeSpan.FromSeconds(MaxDelaySeconds)
        );
    }
}
