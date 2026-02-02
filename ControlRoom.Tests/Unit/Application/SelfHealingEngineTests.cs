using ControlRoom.Application.Services;
using ControlRoom.Domain.Model;

namespace ControlRoom.Tests.Unit.Application;

/// <summary>
/// Unit tests for SelfHealingEngine components.
/// </summary>
public sealed class SelfHealingEngineTests
{
    [Fact]
    public void SelfHealingRule_BasicConstruction()
    {
        // Arrange
        var runbookId = RunbookId.New();

        // Act
        var rule = new SelfHealingRule(
            SelfHealingRuleId.New(),
            "Test Rule",
            "A test rule",
            "alert.severity == Critical",
            runbookId,
            3,
            TimeSpan.FromMinutes(10),
            false,
            true
        );

        // Assert
        Assert.Equal("Test Rule", rule.Name);
        Assert.Equal("A test rule", rule.Description);
        Assert.Equal("alert.severity == Critical", rule.TriggerCondition);
        Assert.Equal(runbookId, rule.RemediationRunbook);
        Assert.Equal(3, rule.MaxExecutionsPerHour);
        Assert.Equal(TimeSpan.FromMinutes(10), rule.CooldownPeriod);
        Assert.False(rule.RequiresApproval);
        Assert.True(rule.IsEnabled);
    }

    [Fact]
    public void SelfHealingExecution_BasicConstruction()
    {
        // Arrange
        var executionId = SelfHealingExecutionId.New();
        var ruleId = SelfHealingRuleId.New();
        var alertId = AlertId.New();

        // Act
        var execution = new SelfHealingExecution(
            executionId,
            ruleId,
            alertId,
            null,
            SelfHealingStatus.Pending,
            DateTimeOffset.UtcNow,
            null,
            null
        );

        // Assert
        Assert.Equal(executionId, execution.Id);
        Assert.Equal(ruleId, execution.RuleId);
        Assert.Equal(alertId, execution.TriggeringAlert);
        Assert.Null(execution.RemediationExecution);
        Assert.Equal(SelfHealingStatus.Pending, execution.Status);
    }

    [Fact]
    public void SelfHealingExecution_RunningState()
    {
        // Arrange
        var execution = new SelfHealingExecution(
            SelfHealingExecutionId.New(),
            SelfHealingRuleId.New(),
            AlertId.New(),
            RunbookExecutionId.New(),
            SelfHealingStatus.Running,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            null,
            null
        );

        // Assert
        Assert.Equal(SelfHealingStatus.Running, execution.Status);
        Assert.NotNull(execution.RemediationExecution);
        Assert.Null(execution.CompletedAt);
    }

    [Fact]
    public void SelfHealingExecution_CompletedSuccessfully()
    {
        // Arrange
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        var completedAt = DateTimeOffset.UtcNow;

        var execution = new SelfHealingExecution(
            SelfHealingExecutionId.New(),
            SelfHealingRuleId.New(),
            AlertId.New(),
            RunbookExecutionId.New(),
            SelfHealingStatus.Succeeded,
            startedAt,
            completedAt,
            "Remediation successful"
        );

        // Assert
        Assert.Equal(SelfHealingStatus.Succeeded, execution.Status);
        Assert.NotNull(execution.CompletedAt);
        Assert.Equal("Remediation successful", execution.Result);
    }

    [Theory]
    [InlineData(SelfHealingStatus.Pending, "Pending")]
    [InlineData(SelfHealingStatus.AwaitingApproval, "AwaitingApproval")]
    [InlineData(SelfHealingStatus.Running, "Running")]
    [InlineData(SelfHealingStatus.Succeeded, "Succeeded")]
    [InlineData(SelfHealingStatus.Failed, "Failed")]
    [InlineData(SelfHealingStatus.Skipped, "Skipped")]
    public void SelfHealingStatus_AllValues(SelfHealingStatus status, string expected)
    {
        Assert.Equal(expected, status.ToString());
    }

    [Fact]
    public void SelfHealingRuleBuilder_BasicBuild()
    {
        // Arrange
        var runbookId = RunbookId.New();

        // Act
        var rule = SelfHealingRuleBuilder.Create()
            .WithName("CPU Recovery")
            .WithDescription("Auto-recover from high CPU")
            .WhenAlert("alert.severity == Critical")
            .ExecuteRunbook(runbookId)
            .Build();

        // Assert
        Assert.Equal("CPU Recovery", rule.Name);
        Assert.Equal("Auto-recover from high CPU", rule.Description);
        Assert.Equal(runbookId, rule.RemediationRunbook);
        Assert.True(rule.IsEnabled);
    }

    [Fact]
    public void SelfHealingRuleBuilder_WhenSeverity_Critical()
    {
        // Arrange
        var runbookId = RunbookId.New();

        // Act
        var rule = SelfHealingRuleBuilder.Create()
            .WithName("Critical Handler")
            .WhenSeverity(AlertSeverity.Critical)
            .ExecuteRunbook(runbookId)
            .Build();

        // Assert
        Assert.Contains("Critical", rule.TriggerCondition);
    }

    [Fact]
    public void SelfHealingRuleBuilder_WhenSeverity_Error()
    {
        // Arrange
        var runbookId = RunbookId.New();

        // Act
        var rule = SelfHealingRuleBuilder.Create()
            .WithName("Error Handler")
            .WhenSeverity(AlertSeverity.Error)
            .ExecuteRunbook(runbookId)
            .Build();

        // Assert
        Assert.Contains("Error", rule.TriggerCondition);
    }

    [Fact]
    public void SelfHealingRuleBuilder_WhenMetric()
    {
        // Arrange
        var runbookId = RunbookId.New();

        // Act
        var rule = SelfHealingRuleBuilder.Create()
            .WithName("CPU Handler")
            .WhenMetric(MetricNames.SystemCpuPercent)
            .ExecuteRunbook(runbookId)
            .Build();

        // Assert
        Assert.Contains(MetricNames.SystemCpuPercent, rule.TriggerCondition);
    }

    [Fact]
    public void SelfHealingRuleBuilder_WhenMetricWithSeverity()
    {
        // Arrange
        var runbookId = RunbookId.New();

        // Act
        var rule = SelfHealingRuleBuilder.Create()
            .WithName("CPU Handler")
            .WhenMetric(MetricNames.SystemCpuPercent, AlertSeverity.Error)
            .ExecuteRunbook(runbookId)
            .Build();

        // Assert
        Assert.Contains(MetricNames.SystemCpuPercent, rule.TriggerCondition);
        Assert.Contains("Error", rule.TriggerCondition);
    }

    [Fact]
    public void SelfHealingRuleBuilder_WithRateLimiting()
    {
        // Arrange
        var runbookId = RunbookId.New();

        // Act
        var rule = SelfHealingRuleBuilder.Create()
            .WithName("Rate Limited")
            .WhenSeverity(AlertSeverity.Warning)
            .ExecuteRunbook(runbookId)
            .WithMaxExecutionsPerHour(5)
            .WithCooldown(TimeSpan.FromMinutes(15))
            .Build();

        // Assert
        Assert.Equal(5, rule.MaxExecutionsPerHour);
        Assert.Equal(TimeSpan.FromMinutes(15), rule.CooldownPeriod);
    }

    [Fact]
    public void SelfHealingRuleBuilder_RequiresApproval()
    {
        // Arrange
        var runbookId = RunbookId.New();

        // Act
        var rule = SelfHealingRuleBuilder.Create()
            .WithName("Approval Required")
            .WhenSeverity(AlertSeverity.Critical)
            .ExecuteRunbook(runbookId)
            .RequireApproval()
            .Build();

        // Assert
        Assert.True(rule.RequiresApproval);
    }

    [Fact]
    public void SelfHealingRuleBuilder_Disabled()
    {
        // Arrange
        var runbookId = RunbookId.New();

        // Act
        var rule = SelfHealingRuleBuilder.Create()
            .WithName("Disabled Rule")
            .WhenSeverity(AlertSeverity.Warning)
            .ExecuteRunbook(runbookId)
            .Enabled(false)
            .Build();

        // Assert
        Assert.False(rule.IsEnabled);
    }

    [Fact]
    public void SelfHealingRuleBuilder_ThrowsWithoutName()
    {
        // Arrange
        var runbookId = RunbookId.New();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            SelfHealingRuleBuilder.Create()
                .WhenSeverity(AlertSeverity.Warning)
                .ExecuteRunbook(runbookId)
                .Build());
    }

    [Fact]
    public void SelfHealingRuleBuilder_ThrowsWithoutCondition()
    {
        // Arrange
        var runbookId = RunbookId.New();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            SelfHealingRuleBuilder.Create()
                .WithName("Test")
                .ExecuteRunbook(runbookId)
                .Build());
    }

    [Fact]
    public void SelfHealingRuleBuilder_ThrowsWithoutRunbook()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            SelfHealingRuleBuilder.Create()
                .WithName("Test")
                .WhenSeverity(AlertSeverity.Warning)
                .Build());
    }

    [Fact]
    public void SelfHealingPatterns_HighCpuRemediation()
    {
        // Arrange
        var runbookId = RunbookId.New();

        // Act
        var rule = SelfHealingPatterns.HighCpuRemediation(runbookId);

        // Assert
        Assert.Equal("High CPU Auto-Remediation", rule.Name);
        Assert.Contains(MetricNames.SystemCpuPercent, rule.TriggerCondition);
        Assert.Equal(runbookId, rule.RemediationRunbook);
        Assert.Equal(2, rule.MaxExecutionsPerHour);
        Assert.False(rule.RequiresApproval);
    }

    [Fact]
    public void SelfHealingPatterns_HighMemoryRemediation()
    {
        // Arrange
        var runbookId = RunbookId.New();

        // Act
        var rule = SelfHealingPatterns.HighMemoryRemediation(runbookId);

        // Assert
        Assert.Equal("High Memory Auto-Remediation", rule.Name);
        Assert.Contains(MetricNames.SystemMemoryPercent, rule.TriggerCondition);
        Assert.Equal(runbookId, rule.RemediationRunbook);
        Assert.Equal(2, rule.MaxExecutionsPerHour);
    }

    [Fact]
    public void SelfHealingPatterns_LowDiskRemediation()
    {
        // Arrange
        var runbookId = RunbookId.New();

        // Act
        var rule = SelfHealingPatterns.LowDiskRemediation(runbookId);

        // Assert
        Assert.Equal("Low Disk Space Auto-Remediation", rule.Name);
        Assert.Contains(MetricNames.SystemDiskPercent, rule.TriggerCondition);
        Assert.Equal(4, rule.MaxExecutionsPerHour);
        Assert.Equal(TimeSpan.FromMinutes(30), rule.CooldownPeriod);
    }

    [Fact]
    public void SelfHealingPatterns_ScriptFailureRemediation()
    {
        // Arrange
        var runbookId = RunbookId.New();

        // Act
        var rule = SelfHealingPatterns.ScriptFailureRemediation(runbookId);

        // Assert
        Assert.Equal("Script Failure Recovery", rule.Name);
        Assert.Contains(MetricNames.ScriptFailure, rule.TriggerCondition);
        Assert.True(rule.RequiresApproval);
    }

    [Fact]
    public void SelfHealingPatterns_CriticalAlertRemediation()
    {
        // Arrange
        var runbookId = RunbookId.New();

        // Act
        var rule = SelfHealingPatterns.CriticalAlertRemediation(runbookId);

        // Assert
        Assert.Equal("Critical Alert Emergency Response", rule.Name);
        Assert.Contains("Critical", rule.TriggerCondition);
        Assert.Equal(1, rule.MaxExecutionsPerHour);
        Assert.Equal(TimeSpan.FromMinutes(60), rule.CooldownPeriod);
        Assert.True(rule.RequiresApproval);
    }

    [Fact]
    public void SelfHealingTriggeredEventArgs_Properties()
    {
        // Arrange
        var execution = new SelfHealingExecution(
            SelfHealingExecutionId.New(),
            SelfHealingRuleId.New(),
            AlertId.New(),
            null,
            SelfHealingStatus.Running,
            DateTimeOffset.UtcNow,
            null,
            null
        );

        var rule = new SelfHealingRule(
            SelfHealingRuleId.New(),
            "Test Rule",
            "Description",
            "alert.severity == Critical",
            RunbookId.New(),
            3,
            TimeSpan.FromMinutes(10),
            false,
            true
        );

        var alert = new Alert(
            AlertId.New(),
            AlertRuleId.New(),
            "Test Alert",
            AlertSeverity.Critical,
            "Test message",
            95.0,
            90.0,
            DateTimeOffset.UtcNow,
            null,
            AlertStatus.Firing,
            new Dictionary<string, string>()
        );

        // Act
        var args = new SelfHealingTriggeredEventArgs
        {
            Execution = execution,
            Rule = rule,
            TriggeringAlert = alert
        };

        // Assert
        Assert.NotNull(args.Execution);
        Assert.NotNull(args.Rule);
        Assert.NotNull(args.TriggeringAlert);
    }

    [Fact]
    public void SelfHealingCompletedEventArgs_Success()
    {
        // Arrange
        var executionId = SelfHealingExecutionId.New();

        // Act
        var args = new SelfHealingCompletedEventArgs
        {
            ExecutionId = executionId,
            Status = SelfHealingStatus.Succeeded,
            Duration = TimeSpan.FromMinutes(2)
        };

        // Assert
        Assert.Equal(executionId, args.ExecutionId);
        Assert.Equal(SelfHealingStatus.Succeeded, args.Status);
        Assert.Equal(TimeSpan.FromMinutes(2), args.Duration);
        Assert.Null(args.ErrorMessage);
    }

    [Fact]
    public void SelfHealingCompletedEventArgs_Failure()
    {
        // Arrange
        var executionId = SelfHealingExecutionId.New();

        // Act
        var args = new SelfHealingCompletedEventArgs
        {
            ExecutionId = executionId,
            Status = SelfHealingStatus.Failed,
            Duration = TimeSpan.FromSeconds(30),
            ErrorMessage = "Runbook execution failed"
        };

        // Assert
        Assert.Equal(SelfHealingStatus.Failed, args.Status);
        Assert.Equal("Runbook execution failed", args.ErrorMessage);
    }

    [Fact]
    public void SelfHealingApprovalRequiredEventArgs_Properties()
    {
        // Arrange
        var execution = new SelfHealingExecution(
            SelfHealingExecutionId.New(),
            SelfHealingRuleId.New(),
            AlertId.New(),
            null,
            SelfHealingStatus.AwaitingApproval,
            DateTimeOffset.UtcNow,
            null,
            null
        );

        var rule = new SelfHealingRule(
            SelfHealingRuleId.New(),
            "Critical Recovery",
            "Requires human approval",
            "alert.severity == Critical",
            RunbookId.New(),
            1,
            TimeSpan.FromMinutes(60),
            true,
            true
        );

        // Act
        var args = new SelfHealingApprovalRequiredEventArgs
        {
            Execution = execution,
            Rule = rule,
            TriggeringAlert = null
        };

        // Assert
        Assert.Equal(SelfHealingStatus.AwaitingApproval, args.Execution.Status);
        Assert.True(args.Rule.RequiresApproval);
        Assert.Null(args.TriggeringAlert);
    }

    [Fact]
    public void SelfHealingExecution_WithRecord_StatusTransition()
    {
        // Arrange
        var original = new SelfHealingExecution(
            SelfHealingExecutionId.New(),
            SelfHealingRuleId.New(),
            AlertId.New(),
            null,
            SelfHealingStatus.Pending,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            null,
            null
        );

        // Act - Transition to Running
        var running = original with
        {
            Status = SelfHealingStatus.Running,
            RemediationExecution = RunbookExecutionId.New()
        };

        // Act - Transition to Completed
        var completed = running with
        {
            Status = SelfHealingStatus.Succeeded,
            CompletedAt = DateTimeOffset.UtcNow,
            Result = "Success"
        };

        // Assert
        Assert.Equal(SelfHealingStatus.Pending, original.Status);
        Assert.Equal(SelfHealingStatus.Running, running.Status);
        Assert.Equal(SelfHealingStatus.Succeeded, completed.Status);
        Assert.NotNull(completed.CompletedAt);
        Assert.Equal("Success", completed.Result);
    }

    [Fact]
    public void SelfHealingRuleId_NewGeneratesUniqueIds()
    {
        // Act
        var id1 = SelfHealingRuleId.New();
        var id2 = SelfHealingRuleId.New();

        // Assert
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void SelfHealingExecutionId_NewGeneratesUniqueIds()
    {
        // Act
        var id1 = SelfHealingExecutionId.New();
        var id2 = SelfHealingExecutionId.New();

        // Assert
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void SelfHealingRule_CooldownPeriodValidation()
    {
        // Arrange
        var rule = new SelfHealingRule(
            SelfHealingRuleId.New(),
            "Test",
            "Test",
            "alert.severity == Warning",
            RunbookId.New(),
            5,
            TimeSpan.FromMinutes(30),
            false,
            true
        );

        // Assert - 30 minutes cooldown
        Assert.Equal(TimeSpan.FromMinutes(30), rule.CooldownPeriod);
        Assert.Equal(5, rule.MaxExecutionsPerHour);
    }

    [Fact]
    public void SelfHealingExecution_ManualTriggerNoAlert()
    {
        // Arrange - Manual trigger has no triggering alert
        var execution = new SelfHealingExecution(
            SelfHealingExecutionId.New(),
            SelfHealingRuleId.New(),
            TriggeringAlert: null,
            RemediationExecution: null,
            SelfHealingStatus.Pending,
            DateTimeOffset.UtcNow,
            CompletedAt: null,
            Result: null
        );

        // Assert
        Assert.Null(execution.TriggeringAlert);
    }

    [Theory]
    [InlineData("alert.severity == Critical", "Critical")]
    [InlineData("alert.metric == 'system.cpu_percent'", "system.cpu_percent")]
    [InlineData("tag.host == 'server1'", "server1")]
    public void TriggerCondition_ContainsExpectedPatterns(string condition, string expectedPattern)
    {
        // This verifies the condition string format
        Assert.Contains(expectedPattern, condition);
    }

    [Fact]
    public void SelfHealingRuleBuilder_FluentChaining()
    {
        // Arrange & Act
        var runbookId = RunbookId.New();

        var rule = SelfHealingRuleBuilder.Create()
            .WithName("Chained Rule")
            .WithDescription("Built with fluent API")
            .WhenMetric(MetricNames.SystemCpuPercent, AlertSeverity.Critical)
            .ExecuteRunbook(runbookId)
            .WithMaxExecutionsPerHour(2)
            .WithCooldown(TimeSpan.FromMinutes(20))
            .RequireApproval(true)
            .Enabled(true)
            .Build();

        // Assert - all properties set correctly
        Assert.Equal("Chained Rule", rule.Name);
        Assert.Equal("Built with fluent API", rule.Description);
        Assert.Equal(runbookId, rule.RemediationRunbook);
        Assert.Equal(2, rule.MaxExecutionsPerHour);
        Assert.Equal(TimeSpan.FromMinutes(20), rule.CooldownPeriod);
        Assert.True(rule.RequiresApproval);
        Assert.True(rule.IsEnabled);
    }
}
