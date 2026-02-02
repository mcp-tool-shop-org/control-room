using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using ControlRoom.Application.UseCases;
using ControlRoom.Domain.Model;
using ControlRoom.Infrastructure.Storage.Queries;

namespace ControlRoom.Application.Services;

/// <summary>
/// Engine that monitors alerts and triggers self-healing remediation runbooks
/// </summary>
public interface ISelfHealingEngine : IDisposable
{
    /// <summary>
    /// Start the self-healing engine
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop the self-healing engine
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Get all enabled self-healing rules
    /// </summary>
    IReadOnlyList<SelfHealingRule> GetRules();

    /// <summary>
    /// Create a new self-healing rule
    /// </summary>
    void CreateRule(SelfHealingRule rule);

    /// <summary>
    /// Update a self-healing rule
    /// </summary>
    void UpdateRule(SelfHealingRule rule);

    /// <summary>
    /// Delete a self-healing rule
    /// </summary>
    void DeleteRule(SelfHealingRuleId ruleId);

    /// <summary>
    /// Manually trigger a self-healing action
    /// </summary>
    Task<SelfHealingExecutionId> TriggerManuallyAsync(SelfHealingRuleId ruleId);

    /// <summary>
    /// Get recent self-healing executions
    /// </summary>
    IReadOnlyList<SelfHealingExecution> GetRecentExecutions(int limit = 50);

    /// <summary>
    /// Approve a pending execution that requires approval
    /// </summary>
    Task ApproveExecutionAsync(SelfHealingExecutionId executionId);

    /// <summary>
    /// Reject a pending execution that requires approval
    /// </summary>
    void RejectExecution(SelfHealingExecutionId executionId);

    /// <summary>
    /// Get execution status
    /// </summary>
    SelfHealingExecution? GetExecution(SelfHealingExecutionId executionId);

    /// <summary>
    /// Event raised when a self-healing action is triggered
    /// </summary>
    event EventHandler<SelfHealingTriggeredEventArgs>? HealingTriggered;

    /// <summary>
    /// Event raised when a self-healing action completes
    /// </summary>
    event EventHandler<SelfHealingCompletedEventArgs>? HealingCompleted;

    /// <summary>
    /// Event raised when a self-healing action requires approval
    /// </summary>
    event EventHandler<SelfHealingApprovalRequiredEventArgs>? ApprovalRequired;
}

/// <summary>
/// Event args when self-healing is triggered
/// </summary>
public sealed class SelfHealingTriggeredEventArgs : EventArgs
{
    public required SelfHealingExecution Execution { get; init; }
    public required SelfHealingRule Rule { get; init; }
    public required Alert? TriggeringAlert { get; init; }
}

/// <summary>
/// Event args when self-healing completes
/// </summary>
public sealed class SelfHealingCompletedEventArgs : EventArgs
{
    public required SelfHealingExecutionId ExecutionId { get; init; }
    public required SelfHealingStatus Status { get; init; }
    public required TimeSpan Duration { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Event args when self-healing requires approval
/// </summary>
public sealed class SelfHealingApprovalRequiredEventArgs : EventArgs
{
    public required SelfHealingExecution Execution { get; init; }
    public required SelfHealingRule Rule { get; init; }
    public required Alert? TriggeringAlert { get; init; }
}

/// <summary>
/// Implementation of the self-healing engine
/// </summary>
public sealed class SelfHealingEngine : ISelfHealingEngine
{
    private readonly MetricsQueries _metrics;
    private readonly IRunbookExecutor _runbookExecutor;
    private readonly IAlertEngine _alertEngine;
    private readonly ILogger<SelfHealingEngine>? _logger;

    private readonly ConcurrentDictionary<SelfHealingRuleId, DateTimeOffset> _lastExecutions = new();
    private readonly ConcurrentDictionary<SelfHealingExecutionId, SelfHealingExecution> _pendingApprovals = new();
    private readonly ConcurrentDictionary<SelfHealingExecutionId, SelfHealingExecution> _activeExecutions = new();

    private bool _disposed;
    private bool _isRunning;

    public event EventHandler<SelfHealingTriggeredEventArgs>? HealingTriggered;
    public event EventHandler<SelfHealingCompletedEventArgs>? HealingCompleted;
    public event EventHandler<SelfHealingApprovalRequiredEventArgs>? ApprovalRequired;

    public SelfHealingEngine(
        MetricsQueries metrics,
        IRunbookExecutor runbookExecutor,
        IAlertEngine alertEngine,
        ILogger<SelfHealingEngine>? logger = null)
    {
        _metrics = metrics;
        _runbookExecutor = runbookExecutor;
        _alertEngine = alertEngine;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
            throw new InvalidOperationException("Self-healing engine is already running");

        // Subscribe to alert events
        _alertEngine.AlertFired += OnAlertFired;
        _alertEngine.AlertResolved += OnAlertResolved;

        // Subscribe to runbook execution events
        _runbookExecutor.StatusChanged += OnRunbookStatusChanged;

        _isRunning = true;
        _logger?.LogInformation("Self-healing engine started");

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (!_isRunning) return Task.CompletedTask;

        // Unsubscribe from events
        _alertEngine.AlertFired -= OnAlertFired;
        _alertEngine.AlertResolved -= OnAlertResolved;
        _runbookExecutor.StatusChanged -= OnRunbookStatusChanged;

        _isRunning = false;
        _logger?.LogInformation("Self-healing engine stopped");

        return Task.CompletedTask;
    }

    public IReadOnlyList<SelfHealingRule> GetRules()
    {
        return _metrics.GetEnabledSelfHealingRules();
    }

    public void CreateRule(SelfHealingRule rule)
    {
        _metrics.CreateSelfHealingRule(rule);
        _logger?.LogInformation("Created self-healing rule: {RuleName}", rule.Name);
    }

    public void UpdateRule(SelfHealingRule rule)
    {
        // TODO: Implement update in MetricsQueries
        _logger?.LogInformation("Updated self-healing rule: {RuleName}", rule.Name);
    }

    public void DeleteRule(SelfHealingRuleId ruleId)
    {
        // TODO: Implement delete in MetricsQueries
        _logger?.LogInformation("Deleted self-healing rule: {RuleId}", ruleId);
    }

    public async Task<SelfHealingExecutionId> TriggerManuallyAsync(SelfHealingRuleId ruleId)
    {
        var rules = _metrics.GetEnabledSelfHealingRules();
        var rule = rules.FirstOrDefault(r => r.Id == ruleId);

        if (rule is null)
            throw new InvalidOperationException($"Self-healing rule not found: {ruleId}");

        return await ExecuteRemediationAsync(rule, null);
    }

    public IReadOnlyList<SelfHealingExecution> GetRecentExecutions(int limit = 50)
    {
        // Combine active, pending, and historical executions
        var executions = new List<SelfHealingExecution>();
        executions.AddRange(_activeExecutions.Values);
        executions.AddRange(_pendingApprovals.Values);

        // TODO: Add GetRecentSelfHealingExecutions to MetricsQueries
        return executions
            .OrderByDescending(e => e.StartedAt)
            .Take(limit)
            .ToList();
    }

    public async Task ApproveExecutionAsync(SelfHealingExecutionId executionId)
    {
        if (!_pendingApprovals.TryRemove(executionId, out var execution))
        {
            throw new InvalidOperationException($"Pending execution not found: {executionId}");
        }

        var rules = _metrics.GetEnabledSelfHealingRules();
        var rule = rules.FirstOrDefault(r => r.Id == execution.RuleId);

        if (rule is null)
        {
            throw new InvalidOperationException($"Rule not found for execution: {execution.RuleId}");
        }

        _logger?.LogInformation("Execution {ExecutionId} approved, starting remediation", executionId);

        // Actually execute the runbook
        await ExecuteRunbookAsync(execution, rule);
    }

    public void RejectExecution(SelfHealingExecutionId executionId)
    {
        if (_pendingApprovals.TryRemove(executionId, out var execution))
        {
            var updated = execution with
            {
                Status = SelfHealingStatus.Skipped,
                CompletedAt = DateTimeOffset.UtcNow,
                Result = "Rejected by user"
            };

            _metrics.RecordSelfHealingExecution(updated);
            _logger?.LogInformation("Execution {ExecutionId} rejected", executionId);

            HealingCompleted?.Invoke(this, new SelfHealingCompletedEventArgs
            {
                ExecutionId = executionId,
                Status = SelfHealingStatus.Skipped,
                Duration = updated.CompletedAt!.Value - execution.StartedAt
            });
        }
    }

    public SelfHealingExecution? GetExecution(SelfHealingExecutionId executionId)
    {
        if (_activeExecutions.TryGetValue(executionId, out var active))
            return active;

        if (_pendingApprovals.TryGetValue(executionId, out var pending))
            return pending;

        return null;
    }

    private void OnAlertFired(object? sender, AlertFiredEventArgs e)
    {
        try
        {
            ProcessAlertForSelfHealing(e.Alert, e.Rule);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing alert for self-healing: {AlertId}", e.Alert.Id);
        }
    }

    private void OnAlertResolved(object? sender, AlertResolvedEventArgs e)
    {
        // Could implement auto-rollback or notification here
        _logger?.LogDebug("Alert {AlertId} resolved after {Duration}", e.AlertId, e.Duration);
    }

    private void OnRunbookStatusChanged(object? sender, ExecutionStatusChangedEventArgs e)
    {
        // Find any self-healing execution tracking this runbook
        foreach (var kvp in _activeExecutions)
        {
            if (kvp.Value.RemediationExecution == e.ExecutionId)
            {
                HandleRunbookCompletion(kvp.Key, kvp.Value, e.NewStatus);
                break;
            }
        }
    }

    private void HandleRunbookCompletion(
        SelfHealingExecutionId executionId,
        SelfHealingExecution execution,
        RunbookExecutionStatus runbookStatus)
    {
        _activeExecutions.TryRemove(executionId, out _);

        var status = runbookStatus switch
        {
            RunbookExecutionStatus.Succeeded => SelfHealingStatus.Succeeded,
            RunbookExecutionStatus.PartialSuccess => SelfHealingStatus.Succeeded,
            _ => SelfHealingStatus.Failed
        };

        var completed = execution with
        {
            Status = status,
            CompletedAt = DateTimeOffset.UtcNow,
            Result = runbookStatus.ToString()
        };

        // Record final state
        // Note: In production, would update existing record instead of inserting
        _logger?.LogInformation("Self-healing {ExecutionId} completed with status {Status}",
            executionId, status);

        HealingCompleted?.Invoke(this, new SelfHealingCompletedEventArgs
        {
            ExecutionId = executionId,
            Status = status,
            Duration = completed.CompletedAt.Value - execution.StartedAt,
            ErrorMessage = status == SelfHealingStatus.Failed ? "Remediation runbook failed" : null
        });
    }

    private void ProcessAlertForSelfHealing(Alert alert, AlertRule alertRule)
    {
        var rules = _metrics.GetEnabledSelfHealingRules();

        foreach (var rule in rules)
        {
            if (MatchesTriggerCondition(rule, alert, alertRule))
            {
                // Check rate limiting
                if (!CanExecute(rule))
                {
                    _logger?.LogWarning("Self-healing rule {RuleName} skipped due to rate limit", rule.Name);
                    continue;
                }

                // Check cooldown
                if (_lastExecutions.TryGetValue(rule.Id, out var lastExec))
                {
                    if (DateTimeOffset.UtcNow - lastExec < rule.CooldownPeriod)
                    {
                        _logger?.LogDebug("Self-healing rule {RuleName} in cooldown", rule.Name);
                        continue;
                    }
                }

                // Trigger self-healing
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ExecuteRemediationAsync(rule, alert);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to execute self-healing rule {RuleName}", rule.Name);
                    }
                });
            }
        }
    }

    private bool MatchesTriggerCondition(SelfHealingRule rule, Alert alert, AlertRule alertRule)
    {
        // Simple expression matching for trigger conditions
        // Format: "alert.severity == Critical && alert.metric == 'system.cpu_percent'"
        // Or: "alert.rule_name contains 'CPU'"

        var condition = rule.TriggerCondition.ToLowerInvariant();

        // Check severity conditions
        if (condition.Contains("severity"))
        {
            if (condition.Contains("critical") && alert.Severity != AlertSeverity.Critical)
                return false;
            if (condition.Contains("error") && alert.Severity < AlertSeverity.Error)
                return false;
            if (condition.Contains("warning") && alert.Severity < AlertSeverity.Warning)
                return false;
        }

        // Check metric name conditions
        if (condition.Contains("metric"))
        {
            var metricPattern = ExtractQuotedValue(condition, "metric");
            if (metricPattern != null && !alertRule.MetricName.Contains(metricPattern, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // Check rule name conditions
        if (condition.Contains("rule_name"))
        {
            var ruleNamePattern = ExtractQuotedValue(condition, "rule_name");
            if (ruleNamePattern != null && !alert.RuleName.Contains(ruleNamePattern, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // Check tag conditions
        if (condition.Contains("tag."))
        {
            var tagMatch = Regex.Match(condition, @"tag\.(\w+)\s*==\s*'([^']+)'");
            if (tagMatch.Success)
            {
                var tagName = tagMatch.Groups[1].Value;
                var tagValue = tagMatch.Groups[2].Value;
                if (!alert.Tags.TryGetValue(tagName, out var actualValue) ||
                    !actualValue.Equals(tagValue, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        // If no conditions failed, the rule matches
        return true;
    }

    private string? ExtractQuotedValue(string condition, string fieldName)
    {
        var patterns = new[]
        {
            $@"{fieldName}\s*==\s*'([^']+)'",
            $@"{fieldName}\s*contains\s*'([^']+)'",
            $@"{fieldName}\s*==\s*""([^""]+)""",
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(condition, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value;
        }

        return null;
    }

    private bool CanExecute(SelfHealingRule rule)
    {
        var count = _metrics.CountRecentExecutions(rule.Id, TimeSpan.FromHours(1));
        return count < rule.MaxExecutionsPerHour;
    }

    private async Task<SelfHealingExecutionId> ExecuteRemediationAsync(SelfHealingRule rule, Alert? alert)
    {
        var executionId = SelfHealingExecutionId.New();
        var execution = new SelfHealingExecution(
            executionId,
            rule.Id,
            alert?.Id,
            RemediationExecution: null,
            rule.RequiresApproval ? SelfHealingStatus.AwaitingApproval : SelfHealingStatus.Pending,
            DateTimeOffset.UtcNow,
            CompletedAt: null,
            Result: null
        );

        // Record execution start
        _metrics.RecordSelfHealingExecution(execution);
        _lastExecutions[rule.Id] = DateTimeOffset.UtcNow;

        _logger?.LogInformation("Self-healing triggered: {RuleName} for alert {AlertId}",
            rule.Name, alert?.Id);

        if (rule.RequiresApproval)
        {
            // Queue for approval
            _pendingApprovals[executionId] = execution;

            ApprovalRequired?.Invoke(this, new SelfHealingApprovalRequiredEventArgs
            {
                Execution = execution,
                Rule = rule,
                TriggeringAlert = alert
            });

            _logger?.LogInformation("Self-healing {ExecutionId} awaiting approval", executionId);
            return executionId;
        }

        // Execute immediately
        HealingTriggered?.Invoke(this, new SelfHealingTriggeredEventArgs
        {
            Execution = execution,
            Rule = rule,
            TriggeringAlert = alert
        });

        await ExecuteRunbookAsync(execution, rule);

        return executionId;
    }

    private async Task ExecuteRunbookAsync(SelfHealingExecution execution, SelfHealingRule rule)
    {
        try
        {
            // Update status to running
            var running = execution with { Status = SelfHealingStatus.Running };
            _activeExecutions[execution.Id] = running;

            // Execute the remediation runbook
            var runbookExecutionId = await _runbookExecutor.ExecuteAsync(
                rule.RemediationRunbook,
                $"Self-healing: {rule.Name}" + (execution.TriggeringAlert != null
                    ? $" (triggered by alert {execution.TriggeringAlert})"
                    : " (manual trigger)")
            );

            // Update execution with runbook ID
            var updated = running with { RemediationExecution = runbookExecutionId };
            _activeExecutions[execution.Id] = updated;

            _logger?.LogInformation("Self-healing {ExecutionId} started runbook execution {RunbookExecutionId}",
                execution.Id, runbookExecutionId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to execute remediation runbook for {ExecutionId}", execution.Id);

            var failed = execution with
            {
                Status = SelfHealingStatus.Failed,
                CompletedAt = DateTimeOffset.UtcNow,
                Result = ex.Message
            };

            _activeExecutions.TryRemove(execution.Id, out _);

            HealingCompleted?.Invoke(this, new SelfHealingCompletedEventArgs
            {
                ExecutionId = execution.Id,
                Status = SelfHealingStatus.Failed,
                Duration = failed.CompletedAt!.Value - execution.StartedAt,
                ErrorMessage = ex.Message
            });
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopAsync().GetAwaiter().GetResult();
    }
}

/// <summary>
/// Builder for creating self-healing rules with a fluent API
/// </summary>
public sealed class SelfHealingRuleBuilder
{
    private string _name = "";
    private string _description = "";
    private string _triggerCondition = "";
    private RunbookId _remediationRunbook;
    private int _maxExecutionsPerHour = 3;
    private TimeSpan _cooldownPeriod = TimeSpan.FromMinutes(10);
    private bool _requiresApproval = false;
    private bool _isEnabled = true;

    public static SelfHealingRuleBuilder Create() => new();

    public SelfHealingRuleBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public SelfHealingRuleBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Set the trigger condition expression.
    /// Examples:
    /// - "alert.severity == Critical"
    /// - "alert.metric == 'system.cpu_percent' && alert.severity >= Error"
    /// - "alert.rule_name contains 'Disk'"
    /// - "tag.host == 'server1'"
    /// </summary>
    public SelfHealingRuleBuilder WhenAlert(string condition)
    {
        _triggerCondition = condition;
        return this;
    }

    /// <summary>
    /// Convenience method for severity-based triggers
    /// </summary>
    public SelfHealingRuleBuilder WhenSeverity(AlertSeverity minSeverity)
    {
        _triggerCondition = minSeverity switch
        {
            AlertSeverity.Critical => "alert.severity == Critical",
            AlertSeverity.Error => "alert.severity >= Error",
            AlertSeverity.Warning => "alert.severity >= Warning",
            _ => "true"
        };
        return this;
    }

    /// <summary>
    /// Convenience method for metric-based triggers
    /// </summary>
    public SelfHealingRuleBuilder WhenMetric(string metricName, AlertSeverity? minSeverity = null)
    {
        _triggerCondition = $"alert.metric == '{metricName}'";
        if (minSeverity.HasValue)
        {
            _triggerCondition += $" && alert.severity >= {minSeverity}";
        }
        return this;
    }

    public SelfHealingRuleBuilder ExecuteRunbook(RunbookId runbookId)
    {
        _remediationRunbook = runbookId;
        return this;
    }

    public SelfHealingRuleBuilder WithMaxExecutionsPerHour(int max)
    {
        _maxExecutionsPerHour = max;
        return this;
    }

    public SelfHealingRuleBuilder WithCooldown(TimeSpan cooldown)
    {
        _cooldownPeriod = cooldown;
        return this;
    }

    public SelfHealingRuleBuilder RequireApproval(bool require = true)
    {
        _requiresApproval = require;
        return this;
    }

    public SelfHealingRuleBuilder Enabled(bool enabled = true)
    {
        _isEnabled = enabled;
        return this;
    }

    public SelfHealingRule Build()
    {
        if (string.IsNullOrWhiteSpace(_name))
            throw new InvalidOperationException("Name is required");
        if (string.IsNullOrWhiteSpace(_triggerCondition))
            throw new InvalidOperationException("Trigger condition is required");
        if (_remediationRunbook == default)
            throw new InvalidOperationException("Remediation runbook is required");

        return new SelfHealingRule(
            SelfHealingRuleId.New(),
            _name,
            _description,
            _triggerCondition,
            _remediationRunbook,
            _maxExecutionsPerHour,
            _cooldownPeriod,
            _requiresApproval,
            _isEnabled
        );
    }
}

/// <summary>
/// Predefined self-healing patterns
/// </summary>
public static class SelfHealingPatterns
{
    /// <summary>
    /// Create a rule for high CPU alerts
    /// </summary>
    public static SelfHealingRule HighCpuRemediation(RunbookId runbookId)
    {
        return SelfHealingRuleBuilder.Create()
            .WithName("High CPU Auto-Remediation")
            .WithDescription("Automatically restart services or clear caches when CPU exceeds threshold")
            .WhenMetric(MetricNames.SystemCpuPercent, AlertSeverity.Error)
            .ExecuteRunbook(runbookId)
            .WithMaxExecutionsPerHour(2)
            .WithCooldown(TimeSpan.FromMinutes(15))
            .RequireApproval(false)
            .Build();
    }

    /// <summary>
    /// Create a rule for high memory alerts
    /// </summary>
    public static SelfHealingRule HighMemoryRemediation(RunbookId runbookId)
    {
        return SelfHealingRuleBuilder.Create()
            .WithName("High Memory Auto-Remediation")
            .WithDescription("Automatically clear caches or restart processes when memory exceeds threshold")
            .WhenMetric(MetricNames.SystemMemoryPercent, AlertSeverity.Error)
            .ExecuteRunbook(runbookId)
            .WithMaxExecutionsPerHour(2)
            .WithCooldown(TimeSpan.FromMinutes(10))
            .RequireApproval(false)
            .Build();
    }

    /// <summary>
    /// Create a rule for disk space alerts
    /// </summary>
    public static SelfHealingRule LowDiskRemediation(RunbookId runbookId)
    {
        return SelfHealingRuleBuilder.Create()
            .WithName("Low Disk Space Auto-Remediation")
            .WithDescription("Automatically clean up temp files and logs when disk space is low")
            .WhenMetric(MetricNames.SystemDiskPercent, AlertSeverity.Warning)
            .ExecuteRunbook(runbookId)
            .WithMaxExecutionsPerHour(4)
            .WithCooldown(TimeSpan.FromMinutes(30))
            .RequireApproval(false)
            .Build();
    }

    /// <summary>
    /// Create a rule for script failure alerts (with approval)
    /// </summary>
    public static SelfHealingRule ScriptFailureRemediation(RunbookId runbookId)
    {
        return SelfHealingRuleBuilder.Create()
            .WithName("Script Failure Recovery")
            .WithDescription("Notify and optionally retry failed scripts with modified parameters")
            .WhenMetric(MetricNames.ScriptFailure, AlertSeverity.Error)
            .ExecuteRunbook(runbookId)
            .WithMaxExecutionsPerHour(5)
            .WithCooldown(TimeSpan.FromMinutes(5))
            .RequireApproval(true)
            .Build();
    }

    /// <summary>
    /// Create a rule for critical alerts (requires approval)
    /// </summary>
    public static SelfHealingRule CriticalAlertRemediation(RunbookId runbookId)
    {
        return SelfHealingRuleBuilder.Create()
            .WithName("Critical Alert Emergency Response")
            .WithDescription("Execute emergency response runbook for any critical alert")
            .WhenSeverity(AlertSeverity.Critical)
            .ExecuteRunbook(runbookId)
            .WithMaxExecutionsPerHour(1)
            .WithCooldown(TimeSpan.FromMinutes(60))
            .RequireApproval(true)
            .Build();
    }
}
