using ControlRoom.Application.UseCases;
using ControlRoom.Domain.Model;

namespace ControlRoom.Tests.Unit.Application;

/// <summary>
/// Unit tests for RunbookExecutor.
/// Note: These tests verify the executor logic without database dependencies.
/// </summary>
public sealed class RunbookExecutorTests
{
    private static ThingId CreateThingId() => new(Guid.NewGuid());

    [Fact]
    public void ExecutionInfo_RecordConstruction()
    {
        // Arrange
        var executionId = RunbookExecutionId.New();
        var runbookId = RunbookId.New();
        var stepStatuses = new Dictionary<string, StepExecutionStatus>
        {
            ["step1"] = StepExecutionStatus.Succeeded,
            ["step2"] = StepExecutionStatus.Running
        };

        // Act
        var info = new RunbookExecutionInfo(
            executionId,
            runbookId,
            RunbookExecutionStatus.Running,
            DateTimeOffset.UtcNow,
            stepStatuses,
            IsPaused: false
        );

        // Assert
        Assert.Equal(executionId, info.ExecutionId);
        Assert.Equal(runbookId, info.RunbookId);
        Assert.Equal(RunbookExecutionStatus.Running, info.Status);
        Assert.Equal(2, info.StepStatuses.Count);
        Assert.False(info.IsPaused);
    }

    [Fact]
    public void StepCompletedEventArgs_Properties()
    {
        // Arrange
        var runId = RunId.New();

        // Act
        var args = new StepCompletedEventArgs
        {
            ExecutionId = RunbookExecutionId.New(),
            StepId = "step1",
            StepName = "Build",
            Status = StepExecutionStatus.Succeeded,
            RunId = runId,
            Duration = TimeSpan.FromSeconds(30),
            ErrorMessage = null
        };

        // Assert
        Assert.Equal("step1", args.StepId);
        Assert.Equal("Build", args.StepName);
        Assert.Equal(StepExecutionStatus.Succeeded, args.Status);
        Assert.Equal(runId, args.RunId);
        Assert.Equal(30, args.Duration?.TotalSeconds);
        Assert.Null(args.ErrorMessage);
    }

    [Fact]
    public void StepCompletedEventArgs_WithError()
    {
        // Arrange & Act
        var args = new StepCompletedEventArgs
        {
            ExecutionId = RunbookExecutionId.New(),
            StepId = "step1",
            StepName = "Deploy",
            Status = StepExecutionStatus.Failed,
            RunId = null,
            Duration = TimeSpan.FromMinutes(2),
            ErrorMessage = "Connection timeout"
        };

        // Assert
        Assert.Equal(StepExecutionStatus.Failed, args.Status);
        Assert.Null(args.RunId);
        Assert.Equal("Connection timeout", args.ErrorMessage);
    }

    [Fact]
    public void ExecutionStatusChangedEventArgs_Properties()
    {
        // Act
        var args = new ExecutionStatusChangedEventArgs
        {
            ExecutionId = RunbookExecutionId.New(),
            OldStatus = RunbookExecutionStatus.Running,
            NewStatus = RunbookExecutionStatus.Paused
        };

        // Assert
        Assert.Equal(RunbookExecutionStatus.Running, args.OldStatus);
        Assert.Equal(RunbookExecutionStatus.Paused, args.NewStatus);
    }

    [Fact]
    public void Runbook_LinearDAG_TopologicalOrder()
    {
        // Arrange: A -> B -> C (linear chain)
        var thingId = CreateThingId();
        var runbook = new Runbook(
            RunbookId.New(),
            "Linear Pipeline",
            "A simple linear pipeline",
            new List<RunbookStep>
            {
                new("stepC", "Step C", thingId, "default",
                    StepCondition.OnSuccess, new[] { "stepB" }, null, null, null),
                new("stepA", "Step A", thingId, "default",
                    StepCondition.Always, Array.Empty<string>(), null, null, null),
                new("stepB", "Step B", thingId, "default",
                    StepCondition.OnSuccess, new[] { "stepA" }, null, null, null)
            },
            null,
            true,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow
        );

        // Act
        var order = runbook.GetTopologicalOrder();

        // Assert
        Assert.Equal(3, order.Count);
        Assert.Equal("stepA", order[0].StepId);
        Assert.Equal("stepB", order[1].StepId);
        Assert.Equal("stepC", order[2].StepId);
    }

    [Fact]
    public void Runbook_DiamondDAG_TopologicalOrder()
    {
        // Arrange: Diamond pattern
        //       A
        //      / \
        //     B   C
        //      \ /
        //       D
        var thingId = CreateThingId();
        var runbook = new Runbook(
            RunbookId.New(),
            "Diamond Pipeline",
            "A diamond-shaped dependency graph",
            new List<RunbookStep>
            {
                new("stepA", "Step A", thingId, "default",
                    StepCondition.Always, Array.Empty<string>(), null, null, null),
                new("stepB", "Step B", thingId, "default",
                    StepCondition.OnSuccess, new[] { "stepA" }, null, null, null),
                new("stepC", "Step C", thingId, "default",
                    StepCondition.OnSuccess, new[] { "stepA" }, null, null, null),
                new("stepD", "Step D", thingId, "default",
                    StepCondition.OnSuccess, new[] { "stepB", "stepC" }, null, null, null)
            },
            null,
            true,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow
        );

        // Act
        var order = runbook.GetTopologicalOrder();
        var orderList = order.ToList();

        // Assert
        Assert.Equal(4, order.Count);
        Assert.Equal("stepA", order[0].StepId); // A must be first

        // B and C can be in any order, but both before D
        var bIndex = orderList.FindIndex(s => s.StepId == "stepB");
        var cIndex = orderList.FindIndex(s => s.StepId == "stepC");
        var dIndex = orderList.FindIndex(s => s.StepId == "stepD");

        Assert.True(bIndex > 0 && bIndex < dIndex);
        Assert.True(cIndex > 0 && cIndex < dIndex);
        Assert.Equal(3, dIndex); // D must be last
    }

    [Fact]
    public void Runbook_ParallelSteps_EntryPoints()
    {
        // Arrange: Multiple independent entry points
        var thingId = CreateThingId();
        var runbook = new Runbook(
            RunbookId.New(),
            "Parallel Start",
            "Multiple parallel starting points",
            new List<RunbookStep>
            {
                new("build-frontend", "Build Frontend", thingId, "default",
                    StepCondition.Always, Array.Empty<string>(), null, null, null),
                new("build-backend", "Build Backend", thingId, "default",
                    StepCondition.Always, Array.Empty<string>(), null, null, null),
                new("build-worker", "Build Worker", thingId, "default",
                    StepCondition.Always, Array.Empty<string>(), null, null, null),
                new("deploy", "Deploy All", thingId, "default",
                    StepCondition.OnSuccess, new[] { "build-frontend", "build-backend", "build-worker" },
                    null, null, null)
            },
            null,
            true,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow
        );

        // Act
        var entryPoints = runbook.GetEntryPoints();
        var dependents = runbook.GetDependents("build-frontend");

        // Assert
        Assert.Equal(3, entryPoints.Count);
        Assert.Contains(entryPoints, s => s.StepId == "build-frontend");
        Assert.Contains(entryPoints, s => s.StepId == "build-backend");
        Assert.Contains(entryPoints, s => s.StepId == "build-worker");

        Assert.Single(dependents);
        Assert.Equal("deploy", dependents[0].StepId);
    }

    [Fact]
    public void RunbookStep_ShouldExecute_NoDependencies()
    {
        // Arrange
        var step = new RunbookStep(
            "start", "Start", CreateThingId(), "default",
            StepCondition.Always, Array.Empty<string>(), null, null, null);

        // Act & Assert
        Assert.True(step.ShouldExecute(new Dictionary<string, StepExecutionStatus>()));
    }

    [Fact]
    public void RunbookStep_ShouldExecute_WaitingForDependency()
    {
        // Arrange
        var step = new RunbookStep(
            "step2", "Step 2", CreateThingId(), "default",
            StepCondition.OnSuccess, new[] { "step1" }, null, null, null);

        // Dependency not yet complete
        var results = new Dictionary<string, StepExecutionStatus>
        {
            ["step1"] = StepExecutionStatus.Running
        };

        // Act & Assert
        Assert.False(step.ShouldExecute(results));
    }

    [Fact]
    public void RunbookStep_OnFailure_ExecutesWhenAnyFails()
    {
        // Arrange - cleanup step that runs on failure
        var step = new RunbookStep(
            "cleanup", "Cleanup", CreateThingId(), "default",
            StepCondition.OnFailure, new[] { "deploy", "verify" }, null, null, null);

        // One dependency failed
        var results = new Dictionary<string, StepExecutionStatus>
        {
            ["deploy"] = StepExecutionStatus.Succeeded,
            ["verify"] = StepExecutionStatus.Failed
        };

        // Act & Assert
        Assert.True(step.ShouldExecute(results));
    }

    [Fact]
    public void RunbookStep_OnFailure_SkipsWhenAllSucceed()
    {
        // Arrange - cleanup step that runs on failure
        var step = new RunbookStep(
            "cleanup", "Cleanup", CreateThingId(), "default",
            StepCondition.OnFailure, new[] { "deploy", "verify" }, null, null, null);

        // All dependencies succeeded
        var results = new Dictionary<string, StepExecutionStatus>
        {
            ["deploy"] = StepExecutionStatus.Succeeded,
            ["verify"] = StepExecutionStatus.Succeeded
        };

        // Act & Assert
        Assert.False(step.ShouldExecute(results));
    }

    [Fact]
    public void RunbookStep_Always_ExecutesRegardlessOfOutcome()
    {
        // Arrange - notification step that always runs
        var step = new RunbookStep(
            "notify", "Notify", CreateThingId(), "default",
            StepCondition.Always, new[] { "deploy" }, null, null, null);

        // Test with failed dependency
        var failedResults = new Dictionary<string, StepExecutionStatus>
        {
            ["deploy"] = StepExecutionStatus.Failed
        };

        // Test with succeeded dependency
        var successResults = new Dictionary<string, StepExecutionStatus>
        {
            ["deploy"] = StepExecutionStatus.Succeeded
        };

        // Act & Assert
        Assert.True(step.ShouldExecute(failedResults));
        Assert.True(step.ShouldExecute(successResults));
    }

    [Fact]
    public void RunbookStep_Expression_ComplexCondition()
    {
        // Arrange - step that runs only if test passed but deploy failed
        var step = new RunbookStep(
            "rollback", "Rollback", CreateThingId(), "default",
            StepCondition.FromExpression("test.succeeded AND deploy.failed"),
            new[] { "test", "deploy" }, null, null, null);

        var shouldRun = new Dictionary<string, StepExecutionStatus>
        {
            ["test"] = StepExecutionStatus.Succeeded,
            ["deploy"] = StepExecutionStatus.Failed
        };

        var shouldNotRun = new Dictionary<string, StepExecutionStatus>
        {
            ["test"] = StepExecutionStatus.Failed,
            ["deploy"] = StepExecutionStatus.Failed
        };

        // Act & Assert
        Assert.True(step.ShouldExecute(shouldRun));
        Assert.False(step.ShouldExecute(shouldNotRun));
    }

    [Fact]
    public void RunbookValidation_InvalidRunbook_ReturnsAllErrors()
    {
        // Arrange - runbook with multiple issues
        var runbook = new Runbook(
            RunbookId.New(),
            "",  // Empty name
            "Description",
            new List<RunbookStep>
            {
                new("step1", "Step 1", CreateThingId(), "default",
                    StepCondition.OnSuccess, new[] { "nonexistent" }, null, null, null)
            },
            null,
            true,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow
        );

        // Act
        var result = runbook.Validate();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("name"));
        Assert.Contains(result.Errors, e => e.Contains("non-existent"));
    }

    [Fact]
    public void RetryPolicy_CalculatesBackoffCorrectly()
    {
        // Arrange
        var policy = new RetryPolicy(
            MaxAttempts: 5,
            InitialDelay: TimeSpan.FromSeconds(1),
            BackoffMultiplier: 2.0,
            MaxDelay: TimeSpan.FromMinutes(1)
        );

        // Act & Assert
        Assert.Equal(TimeSpan.Zero, policy.GetDelay(0));
        Assert.Equal(TimeSpan.FromSeconds(1), policy.GetDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(2), policy.GetDelay(2));
        Assert.Equal(TimeSpan.FromSeconds(4), policy.GetDelay(3));
        Assert.Equal(TimeSpan.FromSeconds(8), policy.GetDelay(4));
        Assert.Equal(TimeSpan.FromSeconds(16), policy.GetDelay(5));
    }

    [Fact]
    public void RetryPolicy_CapsAtMaxDelay()
    {
        // Arrange
        var policy = new RetryPolicy(
            MaxAttempts: 10,
            InitialDelay: TimeSpan.FromSeconds(30),
            BackoffMultiplier: 3.0,
            MaxDelay: TimeSpan.FromSeconds(60)
        );

        // Act - 30 * 3^3 = 810 seconds, but capped at 60
        var delay = policy.GetDelay(4);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(60), delay);
    }

    [Fact]
    public void ExecutionStatusValues_AreDistinct()
    {
        // Arrange
        var allStatuses = Enum.GetValues<RunbookExecutionStatus>();

        // Assert - verify status transitions make sense
        Assert.Contains(RunbookExecutionStatus.Pending, allStatuses);
        Assert.Contains(RunbookExecutionStatus.Running, allStatuses);
        Assert.Contains(RunbookExecutionStatus.Paused, allStatuses);
        Assert.Contains(RunbookExecutionStatus.Succeeded, allStatuses);
        Assert.Contains(RunbookExecutionStatus.Failed, allStatuses);
        Assert.Contains(RunbookExecutionStatus.Canceled, allStatuses);
        Assert.Contains(RunbookExecutionStatus.PartialSuccess, allStatuses);
        Assert.Equal(7, allStatuses.Length);
    }

    [Fact]
    public void StepExecutionStatusValues_AreDistinct()
    {
        // Arrange
        var allStatuses = Enum.GetValues<StepExecutionStatus>();

        // Assert
        Assert.Contains(StepExecutionStatus.Pending, allStatuses);
        Assert.Contains(StepExecutionStatus.Waiting, allStatuses);
        Assert.Contains(StepExecutionStatus.Running, allStatuses);
        Assert.Contains(StepExecutionStatus.Succeeded, allStatuses);
        Assert.Contains(StepExecutionStatus.Failed, allStatuses);
        Assert.Contains(StepExecutionStatus.Skipped, allStatuses);
        Assert.Contains(StepExecutionStatus.Canceled, allStatuses);
        Assert.Equal(7, allStatuses.Length);
    }
}
