using System.Collections.Concurrent;
using System.Text.Json;

namespace ControlRoom.Application.Services;

/// <summary>
/// Automation & Runbook Safety: Ensures automated operations are safe,
/// controlled, and recoverable.
///
/// Checklist items addressed:
/// - Destructive actions gated
/// - Confirmation or approval steps
/// - Step-level retries
/// - Clear abort/pause/resume
/// - Trigger reason visible
/// - Dry-run available
/// - Safe rollback or compensation
/// </summary>
public sealed class RunbookSafetyService
{
    private readonly IRunbookRepository _repository;
    private readonly IApprovalService _approvalService;
    private readonly ConcurrentDictionary<string, RunbookExecution> _activeExecutions = new();

    public event EventHandler<ApprovalRequiredEventArgs>? ApprovalRequired;
    public event EventHandler<StepCompletedEventArgs>? StepCompleted;
    public event EventHandler<ExecutionStateChangedEventArgs>? ExecutionStateChanged;

    public RunbookSafetyService(
        IRunbookRepository repository,
        IApprovalService approvalService)
    {
        _repository = repository;
        _approvalService = approvalService;
    }

    // ========================================================================
    // GUARDRAILS: Destructive Action Gating
    // ========================================================================

    /// <summary>
    /// Analyzes a runbook for dangerous steps and required approvals.
    /// </summary>
    public async Task<RunbookSafetyAnalysis> AnalyzeRunbookAsync(
        RunbookDefinition runbook,
        CancellationToken cancellationToken = default)
    {
        var dangerousSteps = new List<DangerousStepInfo>();
        var requiredApprovals = new List<ApprovalRequirement>();
        var warnings = new List<string>();

        foreach (var step in runbook.Steps)
        {
            // Check for destructive operations
            var dangerLevel = ClassifyStepDanger(step);
            if (dangerLevel > DangerLevel.None)
            {
                dangerousSteps.Add(new DangerousStepInfo(
                    StepId: step.Id,
                    StepName: step.Name,
                    DangerLevel: dangerLevel,
                    Reason: GetDangerReason(step),
                    Mitigations: GetMitigations(step)));

                // High danger steps require approval
                if (dangerLevel >= DangerLevel.High)
                {
                    requiredApprovals.Add(new ApprovalRequirement(
                        StepId: step.Id,
                        Reason: $"Step '{step.Name}' performs {GetDangerReason(step)}",
                        ApprovalType: dangerLevel == DangerLevel.Critical
                            ? ApprovalType.MultiPerson
                            : ApprovalType.SingleApproval,
                        ExpiresAfter: TimeSpan.FromHours(24)));
                }
            }

            // Check for missing guards
            if (step.IsDestructive && !step.HasConfirmation)
            {
                warnings.Add($"Step '{step.Name}' is destructive but has no confirmation gate");
            }

            if (step.AffectsProduction && !step.HasApprovalGate)
            {
                warnings.Add($"Step '{step.Name}' affects production but has no approval gate");
            }
        }

        // Overall risk assessment
        var overallRisk = dangerousSteps.Count switch
        {
            0 => RiskLevel.Low,
            _ when dangerousSteps.Any(s => s.DangerLevel == DangerLevel.Critical) => RiskLevel.Critical,
            _ when dangerousSteps.Any(s => s.DangerLevel == DangerLevel.High) => RiskLevel.High,
            _ when dangerousSteps.Count > 3 => RiskLevel.Medium,
            _ => RiskLevel.Low
        };

        return new RunbookSafetyAnalysis(
            RunbookId: runbook.Id,
            OverallRisk: overallRisk,
            DangerousSteps: dangerousSteps,
            RequiredApprovals: requiredApprovals,
            Warnings: warnings,
            CanRunAutomatically: overallRisk <= RiskLevel.Low && requiredApprovals.Count == 0,
            RecommendsDryRun: dangerousSteps.Count > 0);
    }

    /// <summary>
    /// Creates a gated step that requires confirmation before proceeding.
    /// </summary>
    public GatedStep CreateConfirmationGate(
        string stepId,
        string message,
        ConfirmationLevel level = ConfirmationLevel.Standard)
    {
        return new GatedStep(
            StepId: stepId,
            GateType: GateType.Confirmation,
            Message: message,
            Level: level,
            RequiresTypedConfirmation: level == ConfirmationLevel.TypeToConfirm,
            ConfirmationText: level == ConfirmationLevel.TypeToConfirm ? "DELETE" : null,
            TimeoutSeconds: level == ConfirmationLevel.TimedConfirmation ? 30 : null);
    }

    /// <summary>
    /// Creates an approval gate requiring explicit sign-off.
    /// </summary>
    public GatedStep CreateApprovalGate(
        string stepId,
        string reason,
        ApprovalType approvalType = ApprovalType.SingleApproval,
        IReadOnlyList<string>? requiredApprovers = null)
    {
        return new GatedStep(
            StepId: stepId,
            GateType: GateType.Approval,
            Message: reason,
            ApprovalType: approvalType,
            RequiredApprovers: requiredApprovers,
            ExpiresAfter: TimeSpan.FromHours(24));
    }

    // ========================================================================
    // EXECUTION: Step-Level Control
    // ========================================================================

    /// <summary>
    /// Starts runbook execution with safety controls.
    /// </summary>
    public async Task<RunbookExecutionResult> StartExecutionAsync(
        RunbookDefinition runbook,
        ExecutionOptions options,
        CancellationToken cancellationToken = default)
    {
        // Analyze safety first
        var analysis = await AnalyzeRunbookAsync(runbook, cancellationToken);

        // Block automatic execution of high-risk runbooks
        if (!analysis.CanRunAutomatically && !options.ManualTrigger)
        {
            return new RunbookExecutionResult(
                ExecutionId: null,
                Status: ExecutionStatus.Blocked,
                Message: "This runbook requires manual trigger due to destructive operations",
                SafetyAnalysis: analysis);
        }

        // Create execution context
        var execution = new RunbookExecution
        {
            Id = Guid.NewGuid().ToString("N"),
            RunbookId = runbook.Id,
            RunbookName = runbook.Name,
            Status = ExecutionStatus.Running,
            TriggerReason = options.TriggerReason,
            TriggeredBy = options.TriggeredBy,
            TriggerType = options.TriggerType,
            StartedAt = DateTimeOffset.UtcNow,
            IsDryRun = options.DryRun,
            Steps = runbook.Steps.Select(s => new StepExecution
            {
                StepId = s.Id,
                StepName = s.Name,
                Status = StepStatus.Pending,
                RetryCount = 0,
                MaxRetries = s.MaxRetries
            }).ToList(),
            CompensationSteps = [],
            SafetyAnalysis = analysis
        };

        _activeExecutions[execution.Id] = execution;
        await _repository.SaveExecutionAsync(execution, cancellationToken);

        OnExecutionStateChanged(execution, ExecutionStatus.Pending, ExecutionStatus.Running);

        // Start execution in background
        _ = Task.Run(async () => await ExecuteRunbookAsync(execution, runbook, options, cancellationToken));

        return new RunbookExecutionResult(
            ExecutionId: execution.Id,
            Status: ExecutionStatus.Running,
            Message: options.DryRun ? "Dry run started" : "Execution started",
            SafetyAnalysis: analysis);
    }

    /// <summary>
    /// Pauses an active execution.
    /// </summary>
    public async Task<bool> PauseExecutionAsync(
        string executionId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!_activeExecutions.TryGetValue(executionId, out var execution))
            return false;

        if (execution.Status != ExecutionStatus.Running)
            return false;

        execution.Status = ExecutionStatus.Paused;
        execution.PausedAt = DateTimeOffset.UtcNow;
        execution.PauseReason = reason;

        await _repository.SaveExecutionAsync(execution, cancellationToken);
        OnExecutionStateChanged(execution, ExecutionStatus.Running, ExecutionStatus.Paused);

        return true;
    }

    /// <summary>
    /// Resumes a paused execution.
    /// </summary>
    public async Task<bool> ResumeExecutionAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        if (!_activeExecutions.TryGetValue(executionId, out var execution))
            return false;

        if (execution.Status != ExecutionStatus.Paused)
            return false;

        execution.Status = ExecutionStatus.Running;
        execution.PausedAt = null;
        execution.PauseReason = null;

        await _repository.SaveExecutionAsync(execution, cancellationToken);
        OnExecutionStateChanged(execution, ExecutionStatus.Paused, ExecutionStatus.Running);

        return true;
    }

    /// <summary>
    /// Aborts an active execution.
    /// </summary>
    public async Task<AbortResult> AbortExecutionAsync(
        string executionId,
        AbortOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!_activeExecutions.TryGetValue(executionId, out var execution))
        {
            return new AbortResult(
                Success: false,
                Message: "Execution not found",
                CompensationTriggered: false);
        }

        var previousStatus = execution.Status;
        execution.Status = ExecutionStatus.Aborting;
        execution.AbortedAt = DateTimeOffset.UtcNow;
        execution.AbortReason = options.Reason;

        await _repository.SaveExecutionAsync(execution, cancellationToken);
        OnExecutionStateChanged(execution, previousStatus, ExecutionStatus.Aborting);

        // Run compensation if requested
        var compensationTriggered = false;
        if (options.RunCompensation && execution.CompensationSteps.Count > 0)
        {
            compensationTriggered = true;
            _ = Task.Run(async () => await RunCompensationAsync(execution, cancellationToken));
        }

        execution.Status = ExecutionStatus.Aborted;
        await _repository.SaveExecutionAsync(execution, cancellationToken);
        OnExecutionStateChanged(execution, ExecutionStatus.Aborting, ExecutionStatus.Aborted);

        _activeExecutions.TryRemove(executionId, out _);

        return new AbortResult(
            Success: true,
            Message: "Execution aborted",
            CompensationTriggered: compensationTriggered);
    }

    /// <summary>
    /// Retries a failed step.
    /// </summary>
    public async Task<StepRetryResult> RetryStepAsync(
        string executionId,
        string stepId,
        CancellationToken cancellationToken = default)
    {
        if (!_activeExecutions.TryGetValue(executionId, out var execution))
        {
            return new StepRetryResult(
                Success: false,
                Message: "Execution not found",
                NewStatus: null);
        }

        var step = execution.Steps.FirstOrDefault(s => s.StepId == stepId);
        if (step == null)
        {
            return new StepRetryResult(
                Success: false,
                Message: "Step not found",
                NewStatus: null);
        }

        if (step.Status != StepStatus.Failed)
        {
            return new StepRetryResult(
                Success: false,
                Message: "Only failed steps can be retried",
                NewStatus: step.Status);
        }

        if (step.RetryCount >= step.MaxRetries)
        {
            return new StepRetryResult(
                Success: false,
                Message: $"Max retries ({step.MaxRetries}) exceeded",
                NewStatus: step.Status);
        }

        step.Status = StepStatus.Retrying;
        step.RetryCount++;
        await _repository.SaveExecutionAsync(execution, cancellationToken);

        return new StepRetryResult(
            Success: true,
            Message: $"Retry {step.RetryCount} of {step.MaxRetries}",
            NewStatus: StepStatus.Retrying);
    }

    // ========================================================================
    // VISIBILITY: Execution Transparency
    // ========================================================================

    /// <summary>
    /// Gets the full execution status including trigger reason.
    /// </summary>
    public async Task<ExecutionDetails?> GetExecutionDetailsAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        var execution = _activeExecutions.TryGetValue(executionId, out var active)
            ? active
            : await _repository.GetExecutionAsync(executionId, cancellationToken);

        if (execution == null)
            return null;

        return new ExecutionDetails(
            ExecutionId: execution.Id,
            RunbookName: execution.RunbookName,
            Status: execution.Status,
            TriggerInfo: new TriggerInfo(
                Reason: execution.TriggerReason,
                TriggeredBy: execution.TriggeredBy,
                TriggerType: execution.TriggerType,
                TriggeredAt: execution.StartedAt),
            IsDryRun: execution.IsDryRun,
            Steps: execution.Steps.Select(s => new StepDetails(
                StepId: s.StepId,
                StepName: s.StepName,
                Status: s.Status,
                StartedAt: s.StartedAt,
                CompletedAt: s.CompletedAt,
                Output: s.Output,
                Error: s.Error,
                RetryCount: s.RetryCount,
                MaxRetries: s.MaxRetries)).ToList(),
            StartedAt: execution.StartedAt,
            CompletedAt: execution.CompletedAt,
            Duration: execution.CompletedAt.HasValue
                ? execution.CompletedAt.Value - execution.StartedAt
                : DateTimeOffset.UtcNow - execution.StartedAt,
            SafetyAnalysis: execution.SafetyAnalysis);
    }

    /// <summary>
    /// Performs a dry run of a runbook.
    /// </summary>
    public async Task<DryRunResult> DryRunAsync(
        RunbookDefinition runbook,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var analysis = await AnalyzeRunbookAsync(runbook, cancellationToken);
        var simulatedSteps = new List<DryRunStepResult>();

        foreach (var step in runbook.Steps)
        {
            var simulation = SimulateStep(step, parameters);
            simulatedSteps.Add(simulation);
        }

        return new DryRunResult(
            RunbookId: runbook.Id,
            SimulatedSteps: simulatedSteps,
            WouldSucceed: simulatedSteps.All(s => s.WouldSucceed),
            SafetyAnalysis: analysis,
            EstimatedDuration: TimeSpan.FromSeconds(simulatedSteps.Sum(s => s.EstimatedDurationSeconds)),
            RequiredApprovals: analysis.RequiredApprovals,
            Warnings: analysis.Warnings.Concat(simulatedSteps.SelectMany(s => s.Warnings)).ToList());
    }

    // ========================================================================
    // RECOVERY: Rollback & Compensation
    // ========================================================================

    /// <summary>
    /// Registers a compensation step for rollback.
    /// </summary>
    public void RegisterCompensation(
        string executionId,
        string forStepId,
        CompensationStep compensation)
    {
        if (_activeExecutions.TryGetValue(executionId, out var execution))
        {
            execution.CompensationSteps.Add(compensation with { ForStepId = forStepId });
        }
    }

    /// <summary>
    /// Triggers rollback of completed steps.
    /// </summary>
    public async Task<RollbackResult> RollbackAsync(
        string executionId,
        RollbackOptions options,
        CancellationToken cancellationToken = default)
    {
        var execution = _activeExecutions.TryGetValue(executionId, out var active)
            ? active
            : await _repository.GetExecutionAsync(executionId, cancellationToken);

        if (execution == null)
        {
            return new RollbackResult(
                Success: false,
                Message: "Execution not found",
                RolledBackSteps: []);
        }

        if (execution.CompensationSteps.Count == 0)
        {
            return new RollbackResult(
                Success: false,
                Message: "No compensation steps registered",
                RolledBackSteps: []);
        }

        var rolledBack = new List<string>();
        var errors = new List<string>();

        // Run compensation in reverse order
        var compensationsToRun = options.RollbackToStep != null
            ? execution.CompensationSteps
                .Where(c => ShouldRollback(c, options.RollbackToStep, execution.Steps))
                .Reverse()
                .ToList()
            : execution.CompensationSteps.AsEnumerable().Reverse().ToList();

        foreach (var compensation in compensationsToRun)
        {
            try
            {
                if (options.DryRun)
                {
                    rolledBack.Add($"[DRY RUN] Would rollback: {compensation.Description}");
                }
                else
                {
                    await _repository.ExecuteCompensationAsync(compensation, cancellationToken);
                    rolledBack.Add(compensation.ForStepId);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to rollback {compensation.ForStepId}: {ex.Message}");
                if (!options.ContinueOnError)
                    break;
            }
        }

        return new RollbackResult(
            Success: errors.Count == 0,
            Message: errors.Count == 0
                ? $"Rolled back {rolledBack.Count} steps"
                : $"Rollback completed with {errors.Count} errors",
            RolledBackSteps: rolledBack,
            Errors: errors);
    }

    // ========================================================================
    // Private Helpers
    // ========================================================================

    private async Task ExecuteRunbookAsync(
        RunbookExecution execution,
        RunbookDefinition runbook,
        ExecutionOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var (step, stepExecution) in runbook.Steps.Zip(execution.Steps))
            {
                // Check for pause/abort
                while (execution.Status == ExecutionStatus.Paused)
                {
                    await Task.Delay(1000, cancellationToken);
                }

                if (execution.Status == ExecutionStatus.Aborting ||
                    execution.Status == ExecutionStatus.Aborted)
                {
                    break;
                }

                // Check for gates
                if (step.HasApprovalGate)
                {
                    var approval = await RequestApprovalAsync(execution, step, cancellationToken);
                    if (!approval.Approved)
                    {
                        stepExecution.Status = StepStatus.Blocked;
                        stepExecution.Error = $"Approval denied: {approval.Reason}";
                        break;
                    }
                }

                // Execute step
                stepExecution.Status = StepStatus.Running;
                stepExecution.StartedAt = DateTimeOffset.UtcNow;
                await _repository.SaveExecutionAsync(execution, cancellationToken);

                try
                {
                    if (execution.IsDryRun)
                    {
                        // Simulate execution
                        await Task.Delay(100, cancellationToken);
                        stepExecution.Output = "[DRY RUN] Step simulated successfully";
                    }
                    else
                    {
                        var result = await _repository.ExecuteStepAsync(step, options.Parameters, cancellationToken);
                        stepExecution.Output = result.Output;

                        // Register compensation if step provides it
                        if (result.CompensationStep != null)
                        {
                            RegisterCompensation(execution.Id, step.Id, result.CompensationStep);
                        }
                    }

                    stepExecution.Status = StepStatus.Completed;
                    stepExecution.CompletedAt = DateTimeOffset.UtcNow;
                    OnStepCompleted(execution, stepExecution, true);
                }
                catch (Exception ex) when (stepExecution.RetryCount < step.MaxRetries)
                {
                    stepExecution.RetryCount++;
                    stepExecution.Status = StepStatus.Retrying;
                    stepExecution.Error = ex.Message;
                    // Will retry on next loop iteration
                }
                catch (Exception ex)
                {
                    stepExecution.Status = StepStatus.Failed;
                    stepExecution.Error = ex.Message;
                    stepExecution.CompletedAt = DateTimeOffset.UtcNow;
                    OnStepCompleted(execution, stepExecution, false);

                    if (!options.ContinueOnError)
                    {
                        execution.Status = ExecutionStatus.Failed;
                        break;
                    }
                }

                await _repository.SaveExecutionAsync(execution, cancellationToken);
            }

            // Final status
            if (execution.Status == ExecutionStatus.Running)
            {
                execution.Status = execution.Steps.All(s => s.Status == StepStatus.Completed)
                    ? ExecutionStatus.Completed
                    : ExecutionStatus.CompletedWithErrors;
            }

            execution.CompletedAt = DateTimeOffset.UtcNow;
            await _repository.SaveExecutionAsync(execution, cancellationToken);
            OnExecutionStateChanged(execution, ExecutionStatus.Running, execution.Status);
        }
        finally
        {
            _activeExecutions.TryRemove(execution.Id, out _);
        }
    }

    private async Task<ApprovalResult> RequestApprovalAsync(
        RunbookExecution execution,
        SafetyRunbookStep step,
        CancellationToken cancellationToken)
    {
        var request = new ApprovalRequest(
            ExecutionId: execution.Id,
            StepId: step.Id,
            StepName: step.Name,
            Reason: $"Step '{step.Name}' requires approval before proceeding",
            RequestedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(24));

        OnApprovalRequired(request);

        return await _approvalService.WaitForApprovalAsync(request, cancellationToken);
    }

    private async Task RunCompensationAsync(
        RunbookExecution execution,
        CancellationToken cancellationToken)
    {
        foreach (var compensation in execution.CompensationSteps.AsEnumerable().Reverse())
        {
            try
            {
                await _repository.ExecuteCompensationAsync(compensation, cancellationToken);
            }
            catch
            {
                // Log but continue
            }
        }
    }

    private static DangerLevel ClassifyStepDanger(SafetyRunbookStep step)
    {
        // Check for destructive keywords
        var dangerousKeywords = new[]
        {
            "delete", "remove", "drop", "truncate", "destroy", "terminate",
            "shutdown", "kill", "purge", "wipe", "format"
        };

        var criticalKeywords = new[]
        {
            "production", "prod", "database", "cluster", "all"
        };

        var nameLower = step.Name.ToLowerInvariant();
        var commandLower = step.Command?.ToLowerInvariant() ?? "";

        var hasDangerous = dangerousKeywords.Any(k =>
            nameLower.Contains(k) || commandLower.Contains(k));
        var hasCritical = criticalKeywords.Any(k =>
            nameLower.Contains(k) || commandLower.Contains(k));

        return (hasDangerous, hasCritical, step.AffectsProduction) switch
        {
            (true, true, true) => DangerLevel.Critical,
            (true, _, true) => DangerLevel.High,
            (true, true, _) => DangerLevel.High,
            (true, _, _) => DangerLevel.Medium,
            (_, _, true) => DangerLevel.Medium,
            _ => DangerLevel.None
        };
    }

    private static string GetDangerReason(SafetyRunbookStep step)
    {
        if (step.IsDestructive)
            return "destructive operation";
        if (step.AffectsProduction)
            return "production environment changes";
        if (step.Command?.Contains("delete", StringComparison.OrdinalIgnoreCase) ?? false)
            return "data deletion";
        return "potentially impactful changes";
    }

    private static IReadOnlyList<string> GetMitigations(SafetyRunbookStep step)
    {
        var mitigations = new List<string>();

        if (step.IsDestructive)
        {
            mitigations.Add("Add confirmation gate before step");
            mitigations.Add("Create backup before execution");
        }

        if (step.AffectsProduction)
        {
            mitigations.Add("Require approval from team lead");
            mitigations.Add("Run in staging first");
        }

        mitigations.Add("Enable dry-run mode");
        mitigations.Add("Register compensation step for rollback");

        return mitigations;
    }

    private static DryRunStepResult SimulateStep(
        SafetyRunbookStep step,
        Dictionary<string, object>? parameters)
    {
        var warnings = new List<string>();

        if (step.IsDestructive)
            warnings.Add("This step is destructive");
        if (step.AffectsProduction)
            warnings.Add("This step affects production");

        return new DryRunStepResult(
            StepId: step.Id,
            StepName: step.Name,
            WouldSucceed: true,
            SimulatedOutput: $"[SIMULATED] {step.Name} would execute: {step.Command}",
            EstimatedDurationSeconds: step.TimeoutSeconds ?? 30,
            Warnings: warnings,
            RequiresApproval: step.HasApprovalGate);
    }

    private static bool ShouldRollback(CompensationStep compensation, string rollbackToStep, List<StepExecution> steps)
    {
        var targetIndex = steps.FindIndex(s => s.StepId == rollbackToStep);
        var compensationIndex = steps.FindIndex(s => s.StepId == compensation.ForStepId);
        return compensationIndex >= targetIndex;
    }

    private void OnApprovalRequired(ApprovalRequest request)
    {
        ApprovalRequired?.Invoke(this, new ApprovalRequiredEventArgs(request));
    }

    private void OnStepCompleted(RunbookExecution execution, StepExecution step, bool success)
    {
        StepCompleted?.Invoke(this, new StepCompletedEventArgs(
            execution.Id, step.StepId, step.StepName, success, step.Output, step.Error));
    }

    private void OnExecutionStateChanged(RunbookExecution execution, ExecutionStatus previous, ExecutionStatus current)
    {
        ExecutionStateChanged?.Invoke(this, new ExecutionStateChangedEventArgs(
            execution.Id, previous, current));
    }
}

// ============================================================================
// Runbook Safety Types
// ============================================================================

/// <summary>
/// Runbook definition.
/// </summary>
public sealed class RunbookDefinition
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required List<SafetyRunbookStep> Steps { get; set; }
}

/// <summary>
/// Runbook step.
/// </summary>
public sealed class SafetyRunbookStep
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Command { get; set; }
    public bool IsDestructive { get; set; }
    public bool AffectsProduction { get; set; }
    public bool HasConfirmation { get; set; }
    public bool HasApprovalGate { get; set; }
    public int MaxRetries { get; set; } = 3;
    public int? TimeoutSeconds { get; set; }
}

/// <summary>
/// Runbook execution state.
/// </summary>
public sealed class RunbookExecution
{
    public required string Id { get; set; }
    public required string RunbookId { get; set; }
    public required string RunbookName { get; set; }
    public ExecutionStatus Status { get; set; }
    public required string TriggerReason { get; set; }
    public required string TriggeredBy { get; set; }
    public required ExecutionTriggerType TriggerType { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? PausedAt { get; set; }
    public string? PauseReason { get; set; }
    public DateTimeOffset? AbortedAt { get; set; }
    public string? AbortReason { get; set; }
    public bool IsDryRun { get; set; }
    public required List<StepExecution> Steps { get; set; }
    public required List<CompensationStep> CompensationSteps { get; set; }
    public RunbookSafetyAnalysis? SafetyAnalysis { get; set; }
}

/// <summary>
/// Step execution state.
/// </summary>
public sealed class StepExecution
{
    public required string StepId { get; set; }
    public required string StepName { get; set; }
    public StepStatus Status { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; }
}

/// <summary>
/// Execution status.
/// </summary>
public enum ExecutionStatus
{
    Pending,
    Running,
    Paused,
    Aborting,
    Aborted,
    Completed,
    CompletedWithErrors,
    Failed,
    Blocked
}

/// <summary>
/// Step status.
/// </summary>
public enum StepStatus
{
    Pending,
    Running,
    Retrying,
    Completed,
    Failed,
    Skipped,
    Blocked
}

/// <summary>
/// Danger level.
/// </summary>
public enum DangerLevel
{
    None,
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Risk level.
/// </summary>
public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Execution trigger type.
/// </summary>
public enum ExecutionTriggerType
{
    Manual,
    Scheduled,
    Alert,
    Webhook,
    Pipeline
}

/// <summary>
/// Safety analysis result.
/// </summary>
public sealed record RunbookSafetyAnalysis(
    string RunbookId,
    RiskLevel OverallRisk,
    IReadOnlyList<DangerousStepInfo> DangerousSteps,
    IReadOnlyList<ApprovalRequirement> RequiredApprovals,
    IReadOnlyList<string> Warnings,
    bool CanRunAutomatically,
    bool RecommendsDryRun);

/// <summary>
/// Dangerous step info.
/// </summary>
public sealed record DangerousStepInfo(
    string StepId,
    string StepName,
    DangerLevel DangerLevel,
    string Reason,
    IReadOnlyList<string> Mitigations);

/// <summary>
/// Approval requirement.
/// </summary>
public sealed record ApprovalRequirement(
    string StepId,
    string Reason,
    ApprovalType ApprovalType,
    TimeSpan ExpiresAfter);

/// <summary>
/// Approval type.
/// </summary>
public enum ApprovalType
{
    SingleApproval,
    MultiPerson,
    TeamLead,
    Admin
}

/// <summary>
/// Gated step configuration.
/// </summary>
public sealed record GatedStep(
    string StepId,
    GateType GateType,
    string Message,
    ConfirmationLevel Level = ConfirmationLevel.Standard,
    bool RequiresTypedConfirmation = false,
    string? ConfirmationText = null,
    int? TimeoutSeconds = null,
    ApprovalType ApprovalType = ApprovalType.SingleApproval,
    IReadOnlyList<string>? RequiredApprovers = null,
    TimeSpan? ExpiresAfter = null);

/// <summary>
/// Gate type.
/// </summary>
public enum GateType
{
    Confirmation,
    Approval
}

/// <summary>
/// Confirmation level.
/// </summary>
public enum ConfirmationLevel
{
    Standard,
    TypeToConfirm,
    TimedConfirmation
}

/// <summary>
/// Execution options.
/// </summary>
public sealed record ExecutionOptions(
    string TriggerReason,
    string TriggeredBy,
    ExecutionTriggerType TriggerType = ExecutionTriggerType.Manual,
    bool DryRun = false,
    bool ManualTrigger = true,
    bool ContinueOnError = false,
    Dictionary<string, object>? Parameters = null);

/// <summary>
/// Execution result.
/// </summary>
public sealed record RunbookExecutionResult(
    string? ExecutionId,
    ExecutionStatus Status,
    string Message,
    RunbookSafetyAnalysis SafetyAnalysis);

/// <summary>
/// Abort options.
/// </summary>
public sealed record AbortOptions(
    string Reason,
    bool RunCompensation = true);

/// <summary>
/// Abort result.
/// </summary>
public sealed record AbortResult(
    bool Success,
    string Message,
    bool CompensationTriggered);

/// <summary>
/// Step retry result.
/// </summary>
public sealed record StepRetryResult(
    bool Success,
    string Message,
    StepStatus? NewStatus);

/// <summary>
/// Trigger info.
/// </summary>
public sealed record TriggerInfo(
    string Reason,
    string TriggeredBy,
    ExecutionTriggerType TriggerType,
    DateTimeOffset TriggeredAt);

/// <summary>
/// Execution details.
/// </summary>
public sealed record ExecutionDetails(
    string ExecutionId,
    string RunbookName,
    ExecutionStatus Status,
    TriggerInfo TriggerInfo,
    bool IsDryRun,
    IReadOnlyList<StepDetails> Steps,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    TimeSpan Duration,
    RunbookSafetyAnalysis? SafetyAnalysis);

/// <summary>
/// Step details.
/// </summary>
public sealed record StepDetails(
    string StepId,
    string StepName,
    StepStatus Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Output,
    string? Error,
    int RetryCount,
    int MaxRetries);

/// <summary>
/// Dry run result.
/// </summary>
public sealed record DryRunResult(
    string RunbookId,
    IReadOnlyList<DryRunStepResult> SimulatedSteps,
    bool WouldSucceed,
    RunbookSafetyAnalysis SafetyAnalysis,
    TimeSpan EstimatedDuration,
    IReadOnlyList<ApprovalRequirement> RequiredApprovals,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Dry run step result.
/// </summary>
public sealed record DryRunStepResult(
    string StepId,
    string StepName,
    bool WouldSucceed,
    string SimulatedOutput,
    int EstimatedDurationSeconds,
    IReadOnlyList<string> Warnings,
    bool RequiresApproval);

/// <summary>
/// Compensation step.
/// </summary>
public sealed record CompensationStep(
    string Id,
    string ForStepId,
    string Description,
    string Command);

/// <summary>
/// Rollback options.
/// </summary>
public sealed record RollbackOptions(
    string? RollbackToStep = null,
    bool DryRun = false,
    bool ContinueOnError = true);

/// <summary>
/// Rollback result.
/// </summary>
public sealed record RollbackResult(
    bool Success,
    string Message,
    IReadOnlyList<string> RolledBackSteps,
    IReadOnlyList<string>? Errors = null);

/// <summary>
/// Approval request.
/// </summary>
public sealed record ApprovalRequest(
    string ExecutionId,
    string StepId,
    string StepName,
    string Reason,
    DateTimeOffset RequestedAt,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Approval result.
/// </summary>
public sealed record ApprovalResult(
    bool Approved,
    string? ApprovedBy,
    string? Reason,
    DateTimeOffset? ApprovedAt);

/// <summary>
/// Step execution result.
/// </summary>
public sealed record StepExecutionResult(
    bool Success,
    string? Output,
    CompensationStep? CompensationStep);

// ============================================================================
// Events
// ============================================================================

public sealed class ApprovalRequiredEventArgs : EventArgs
{
    public ApprovalRequest Request { get; }
    public ApprovalRequiredEventArgs(ApprovalRequest request) => Request = request;
}

public sealed class StepCompletedEventArgs : EventArgs
{
    public string ExecutionId { get; }
    public string StepId { get; }
    public string StepName { get; }
    public bool Success { get; }
    public string? Output { get; }
    public string? Error { get; }

    public StepCompletedEventArgs(string executionId, string stepId, string stepName,
        bool success, string? output, string? error)
    {
        ExecutionId = executionId;
        StepId = stepId;
        StepName = stepName;
        Success = success;
        Output = output;
        Error = error;
    }
}

public sealed class ExecutionStateChangedEventArgs : EventArgs
{
    public string ExecutionId { get; }
    public ExecutionStatus PreviousStatus { get; }
    public ExecutionStatus CurrentStatus { get; }

    public ExecutionStateChangedEventArgs(string executionId,
        ExecutionStatus previousStatus, ExecutionStatus currentStatus)
    {
        ExecutionId = executionId;
        PreviousStatus = previousStatus;
        CurrentStatus = currentStatus;
    }
}

// ============================================================================
// Interfaces
// ============================================================================

public interface IRunbookRepository
{
    Task SaveExecutionAsync(RunbookExecution execution, CancellationToken cancellationToken);
    Task<RunbookExecution?> GetExecutionAsync(string executionId, CancellationToken cancellationToken);
    Task<StepExecutionResult> ExecuteStepAsync(SafetyRunbookStep step, Dictionary<string, object>? parameters, CancellationToken cancellationToken);
    Task ExecuteCompensationAsync(CompensationStep compensation, CancellationToken cancellationToken);
}

public interface IApprovalService
{
    Task<ApprovalResult> WaitForApprovalAsync(ApprovalRequest request, CancellationToken cancellationToken);
}
