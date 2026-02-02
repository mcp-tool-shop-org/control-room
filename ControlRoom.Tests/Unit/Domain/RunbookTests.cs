using ControlRoom.Domain.Model;

namespace ControlRoom.Tests.Unit.Domain;

/// <summary>
/// Unit tests for Runbook domain model
/// </summary>
public sealed class RunbookTests
{
    private static ThingId CreateThingId() => new(Guid.NewGuid());

    [Fact]
    public void Runbook_Validate_EmptyName_ReturnsError()
    {
        // Arrange
        var runbook = new Runbook(
            RunbookId.New(),
            "",
            "Description",
            new List<RunbookStep>
            {
                new("step1", "Step 1", CreateThingId(), "default",
                    StepCondition.Always, Array.Empty<string>(), null, null, null)
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
        Assert.Contains(result.Errors, e => e.Contains("name is required"));
    }

    [Fact]
    public void Runbook_Validate_NoSteps_ReturnsError()
    {
        // Arrange
        var runbook = new Runbook(
            RunbookId.New(),
            "Test Runbook",
            "Description",
            Array.Empty<RunbookStep>(),
            null,
            true,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow
        );

        // Act
        var result = runbook.Validate();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("at least one step"));
    }

    [Fact]
    public void Runbook_Validate_DuplicateStepIds_ReturnsError()
    {
        // Arrange
        var thingId = CreateThingId();
        var runbook = new Runbook(
            RunbookId.New(),
            "Test Runbook",
            "Description",
            new List<RunbookStep>
            {
                new("step1", "Step 1", thingId, "default",
                    StepCondition.Always, Array.Empty<string>(), null, null, null),
                new("step1", "Step 1 Duplicate", thingId, "default",
                    StepCondition.Always, Array.Empty<string>(), null, null, null)
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
        Assert.Contains(result.Errors, e => e.Contains("Duplicate step ID"));
    }

    [Fact]
    public void Runbook_Validate_InvalidDependency_ReturnsError()
    {
        // Arrange
        var runbook = new Runbook(
            RunbookId.New(),
            "Test Runbook",
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
        Assert.Contains(result.Errors, e => e.Contains("non-existent step"));
    }

    [Fact]
    public void Runbook_HasCycle_DirectCycle_ReturnsTrue()
    {
        // Arrange: step1 -> step2 -> step1 (cycle)
        var thingId = CreateThingId();
        var runbook = new Runbook(
            RunbookId.New(),
            "Test Runbook",
            "Description",
            new List<RunbookStep>
            {
                new("step1", "Step 1", thingId, "default",
                    StepCondition.OnSuccess, new[] { "step2" }, null, null, null),
                new("step2", "Step 2", thingId, "default",
                    StepCondition.OnSuccess, new[] { "step1" }, null, null, null)
            },
            null,
            true,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow
        );

        // Act & Assert
        Assert.True(runbook.HasCycle());
    }

    [Fact]
    public void Runbook_HasCycle_IndirectCycle_ReturnsTrue()
    {
        // Arrange: step1 -> step2 -> step3 -> step1 (cycle)
        var thingId = CreateThingId();
        var runbook = new Runbook(
            RunbookId.New(),
            "Test Runbook",
            "Description",
            new List<RunbookStep>
            {
                new("step1", "Step 1", thingId, "default",
                    StepCondition.OnSuccess, new[] { "step3" }, null, null, null),
                new("step2", "Step 2", thingId, "default",
                    StepCondition.OnSuccess, new[] { "step1" }, null, null, null),
                new("step3", "Step 3", thingId, "default",
                    StepCondition.OnSuccess, new[] { "step2" }, null, null, null)
            },
            null,
            true,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow
        );

        // Act & Assert
        Assert.True(runbook.HasCycle());
    }

    [Fact]
    public void Runbook_HasCycle_NoCycle_ReturnsFalse()
    {
        // Arrange: step1 -> step2 -> step3 (no cycle)
        var thingId = CreateThingId();
        var runbook = new Runbook(
            RunbookId.New(),
            "Test Runbook",
            "Description",
            new List<RunbookStep>
            {
                new("step1", "Step 1", thingId, "default",
                    StepCondition.Always, Array.Empty<string>(), null, null, null),
                new("step2", "Step 2", thingId, "default",
                    StepCondition.OnSuccess, new[] { "step1" }, null, null, null),
                new("step3", "Step 3", thingId, "default",
                    StepCondition.OnSuccess, new[] { "step2" }, null, null, null)
            },
            null,
            true,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow
        );

        // Act & Assert
        Assert.False(runbook.HasCycle());
    }

    [Fact]
    public void Runbook_GetTopologicalOrder_ReturnsCorrectOrder()
    {
        // Arrange: step3 depends on step2, step2 depends on step1
        var thingId = CreateThingId();
        var runbook = new Runbook(
            RunbookId.New(),
            "Test Runbook",
            "Description",
            new List<RunbookStep>
            {
                new("step3", "Step 3", thingId, "default",
                    StepCondition.OnSuccess, new[] { "step2" }, null, null, null),
                new("step1", "Step 1", thingId, "default",
                    StepCondition.Always, Array.Empty<string>(), null, null, null),
                new("step2", "Step 2", thingId, "default",
                    StepCondition.OnSuccess, new[] { "step1" }, null, null, null)
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
        Assert.Equal("step1", order[0].StepId);
        Assert.Equal("step2", order[1].StepId);
        Assert.Equal("step3", order[2].StepId);
    }

    [Fact]
    public void Runbook_GetEntryPoints_ReturnsStepsWithNoDependencies()
    {
        // Arrange
        var thingId = CreateThingId();
        var runbook = new Runbook(
            RunbookId.New(),
            "Test Runbook",
            "Description",
            new List<RunbookStep>
            {
                new("step1", "Step 1", thingId, "default",
                    StepCondition.Always, Array.Empty<string>(), null, null, null),
                new("step2", "Step 2", thingId, "default",
                    StepCondition.Always, Array.Empty<string>(), null, null, null),
                new("step3", "Step 3", thingId, "default",
                    StepCondition.OnSuccess, new[] { "step1", "step2" }, null, null, null)
            },
            null,
            true,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow
        );

        // Act
        var entryPoints = runbook.GetEntryPoints();

        // Assert
        Assert.Equal(2, entryPoints.Count);
        Assert.Contains(entryPoints, s => s.StepId == "step1");
        Assert.Contains(entryPoints, s => s.StepId == "step2");
    }

    [Fact]
    public void Runbook_GetDependents_ReturnsCorrectSteps()
    {
        // Arrange
        var thingId = CreateThingId();
        var runbook = new Runbook(
            RunbookId.New(),
            "Test Runbook",
            "Description",
            new List<RunbookStep>
            {
                new("step1", "Step 1", thingId, "default",
                    StepCondition.Always, Array.Empty<string>(), null, null, null),
                new("step2", "Step 2", thingId, "default",
                    StepCondition.OnSuccess, new[] { "step1" }, null, null, null),
                new("step3", "Step 3", thingId, "default",
                    StepCondition.OnSuccess, new[] { "step1" }, null, null, null)
            },
            null,
            true,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow
        );

        // Act
        var dependents = runbook.GetDependents("step1");

        // Assert
        Assert.Equal(2, dependents.Count);
        Assert.Contains(dependents, s => s.StepId == "step2");
        Assert.Contains(dependents, s => s.StepId == "step3");
    }

    [Fact]
    public void RunbookStep_ShouldExecute_OnSuccess_AllSucceeded_ReturnsTrue()
    {
        // Arrange
        var step = new RunbookStep(
            "step3", "Step 3", CreateThingId(), "default",
            StepCondition.OnSuccess, new[] { "step1", "step2" }, null, null, null);

        var results = new Dictionary<string, StepExecutionStatus>
        {
            ["step1"] = StepExecutionStatus.Succeeded,
            ["step2"] = StepExecutionStatus.Succeeded
        };

        // Act & Assert
        Assert.True(step.ShouldExecute(results));
    }

    [Fact]
    public void RunbookStep_ShouldExecute_OnSuccess_OneFailed_ReturnsFalse()
    {
        // Arrange
        var step = new RunbookStep(
            "step3", "Step 3", CreateThingId(), "default",
            StepCondition.OnSuccess, new[] { "step1", "step2" }, null, null, null);

        var results = new Dictionary<string, StepExecutionStatus>
        {
            ["step1"] = StepExecutionStatus.Succeeded,
            ["step2"] = StepExecutionStatus.Failed
        };

        // Act & Assert
        Assert.False(step.ShouldExecute(results));
    }

    [Fact]
    public void RunbookStep_ShouldExecute_OnFailure_OneFailed_ReturnsTrue()
    {
        // Arrange
        var step = new RunbookStep(
            "cleanup", "Cleanup", CreateThingId(), "default",
            StepCondition.OnFailure, new[] { "step1", "step2" }, null, null, null);

        var results = new Dictionary<string, StepExecutionStatus>
        {
            ["step1"] = StepExecutionStatus.Succeeded,
            ["step2"] = StepExecutionStatus.Failed
        };

        // Act & Assert
        Assert.True(step.ShouldExecute(results));
    }

    [Fact]
    public void RunbookStep_ShouldExecute_Always_AllComplete_ReturnsTrue()
    {
        // Arrange
        var step = new RunbookStep(
            "final", "Final", CreateThingId(), "default",
            StepCondition.Always, new[] { "step1", "step2" }, null, null, null);

        var results = new Dictionary<string, StepExecutionStatus>
        {
            ["step1"] = StepExecutionStatus.Succeeded,
            ["step2"] = StepExecutionStatus.Failed
        };

        // Act & Assert
        Assert.True(step.ShouldExecute(results));
    }

    [Fact]
    public void RunbookStep_ShouldExecute_Expression_SimpleCondition()
    {
        // Arrange
        var step = new RunbookStep(
            "conditional", "Conditional", CreateThingId(), "default",
            StepCondition.FromExpression("step1.succeeded AND step2.failed"),
            new[] { "step1", "step2" }, null, null, null);

        var results = new Dictionary<string, StepExecutionStatus>
        {
            ["step1"] = StepExecutionStatus.Succeeded,
            ["step2"] = StepExecutionStatus.Failed
        };

        // Act & Assert
        Assert.True(step.ShouldExecute(results));
    }

    [Fact]
    public void RunbookStep_ShouldExecute_Expression_OrCondition()
    {
        // Arrange
        var step = new RunbookStep(
            "conditional", "Conditional", CreateThingId(), "default",
            StepCondition.FromExpression("step1.failed OR step2.succeeded"),
            new[] { "step1", "step2" }, null, null, null);

        var results = new Dictionary<string, StepExecutionStatus>
        {
            ["step1"] = StepExecutionStatus.Succeeded,
            ["step2"] = StepExecutionStatus.Succeeded
        };

        // Act & Assert
        Assert.True(step.ShouldExecute(results));
    }

    [Fact]
    public void RetryPolicy_GetDelay_ExponentialBackoff()
    {
        // Arrange
        var policy = new RetryPolicy(
            MaxAttempts: 5,
            InitialDelay: TimeSpan.FromSeconds(1),
            BackoffMultiplier: 2.0,
            MaxDelay: TimeSpan.FromSeconds(30));

        // Act & Assert
        Assert.Equal(TimeSpan.Zero, policy.GetDelay(0));
        Assert.Equal(TimeSpan.FromSeconds(1), policy.GetDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(2), policy.GetDelay(2));
        Assert.Equal(TimeSpan.FromSeconds(4), policy.GetDelay(3));
        Assert.Equal(TimeSpan.FromSeconds(8), policy.GetDelay(4));
    }

    [Fact]
    public void RetryPolicy_GetDelay_CapsAtMaxDelay()
    {
        // Arrange
        var policy = new RetryPolicy(
            MaxAttempts: 10,
            InitialDelay: TimeSpan.FromSeconds(10),
            BackoffMultiplier: 10.0,
            MaxDelay: TimeSpan.FromSeconds(30));

        // Act - 10 * 10^2 = 1000 seconds, but capped at 30
        var delay = policy.GetDelay(3);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(30), delay);
    }

    [Fact]
    public void WebhookTrigger_ValidateSignature_ValidSignature()
    {
        // Arrange
        var trigger = new WebhookTrigger("my-secret-key");
        var payload = "{\"event\":\"test\"}";

        // Calculate expected signature
        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes("my-secret-key"));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
        var signature = Convert.ToHexStringLower(hash);

        // Act & Assert
        Assert.True(trigger.ValidateSignature(payload, signature));
        Assert.True(trigger.ValidateSignature(payload, $"sha256={signature}"));
    }

    [Fact]
    public void WebhookTrigger_ValidateSignature_InvalidSignature()
    {
        // Arrange
        var trigger = new WebhookTrigger("my-secret-key");
        var payload = "{\"event\":\"test\"}";

        // Act & Assert
        Assert.False(trigger.ValidateSignature(payload, "invalid-signature"));
    }

    [Fact]
    public void RunbookConfig_Serialization_RoundTrip()
    {
        // Arrange
        var config = new RunbookConfig
        {
            Name = "Deploy Pipeline",
            Description = "Deploy to production",
            Steps = new List<RunbookStepConfig>
            {
                new()
                {
                    StepId = "build",
                    Name = "Build",
                    ThingId = Guid.NewGuid().ToString(),
                    ProfileId = "release",
                    ConditionType = ConditionType.Always,
                    DependsOn = new List<string>()
                },
                new()
                {
                    StepId = "test",
                    Name = "Test",
                    ThingId = Guid.NewGuid().ToString(),
                    ProfileId = "default",
                    ConditionType = ConditionType.OnSuccess,
                    DependsOn = new List<string> { "build" }
                }
            },
            Trigger = new ScheduleTrigger("0 2 * * *"),
            IsEnabled = true
        };

        // Act
        var json = config.ToJson();
        var deserialized = RunbookConfig.Parse(json);

        // Assert
        Assert.Equal(config.Name, deserialized.Name);
        Assert.Equal(config.Description, deserialized.Description);
        Assert.Equal(config.Steps.Count, deserialized.Steps.Count);
        Assert.Equal(config.Steps[0].StepId, deserialized.Steps[0].StepId);
        Assert.Equal(config.Steps[1].DependsOn[0], deserialized.Steps[1].DependsOn[0]);
        Assert.IsType<ScheduleTrigger>(deserialized.Trigger);
        Assert.Equal("0 2 * * *", ((ScheduleTrigger)deserialized.Trigger!).CronExpression);
    }

    [Fact]
    public void RunbookExecution_GetStatusCounts_ReturnsCorrectCounts()
    {
        // Arrange
        var execution = new RunbookExecution(
            RunbookExecutionId.New(),
            RunbookId.New(),
            RunbookExecutionStatus.PartialSuccess,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow,
            new List<StepExecution>
            {
                new("step1", "Step 1", null, StepExecutionStatus.Succeeded,
                    null, null, 1, null, null),
                new("step2", "Step 2", null, StepExecutionStatus.Succeeded,
                    null, null, 1, null, null),
                new("step3", "Step 3", null, StepExecutionStatus.Failed,
                    null, null, 2, "Error", null),
                new("step4", "Step 4", null, StepExecutionStatus.Skipped,
                    null, null, 1, null, null)
            },
            null,
            null
        );

        // Act
        var counts = execution.GetStatusCounts();

        // Assert
        Assert.Equal(2, counts[StepExecutionStatus.Succeeded]);
        Assert.Equal(1, counts[StepExecutionStatus.Failed]);
        Assert.Equal(1, counts[StepExecutionStatus.Skipped]);
    }

    [Fact]
    public void RunbookExecution_Duration_CalculatesCorrectly()
    {
        // Arrange
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var endedAt = DateTimeOffset.UtcNow;

        var execution = new RunbookExecution(
            RunbookExecutionId.New(),
            RunbookId.New(),
            RunbookExecutionStatus.Succeeded,
            startedAt,
            endedAt,
            Array.Empty<StepExecution>(),
            null,
            null
        );

        // Act & Assert
        Assert.NotNull(execution.Duration);
        Assert.Equal(10, execution.Duration.Value.TotalMinutes, 0.1);
    }

    [Fact]
    public void StepCondition_StaticFactories_CreateCorrectTypes()
    {
        // Act & Assert
        Assert.Equal(ConditionType.Always, StepCondition.Always.Type);
        Assert.Equal(ConditionType.OnSuccess, StepCondition.OnSuccess.Type);
        Assert.Equal(ConditionType.OnFailure, StepCondition.OnFailure.Type);

        var expr = StepCondition.FromExpression("step1.succeeded");
        Assert.Equal(ConditionType.Expression, expr.Type);
        Assert.Equal("step1.succeeded", expr.Expression);
    }

    [Fact]
    public void RunbookId_New_CreatesUniqueIds()
    {
        // Act
        var id1 = RunbookId.New();
        var id2 = RunbookId.New();

        // Assert
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void RunbookExecutionId_ToString_ReturnsGuidFormat()
    {
        // Arrange
        var guid = Guid.Parse("12345678-1234-1234-1234-123456789012");
        var id = new RunbookExecutionId(guid);

        // Act
        var str = id.ToString();

        // Assert
        Assert.Equal("12345678-1234-1234-1234-123456789012", str);
    }
}
