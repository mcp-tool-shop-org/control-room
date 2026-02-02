using ControlRoom.Domain.Model;

namespace ControlRoom.Tests.Unit.Domain;

public sealed class MetricsTests
{
    [Fact]
    public void MetricId_New_GeneratesUniqueIds()
    {
        var id1 = MetricId.New();
        var id2 = MetricId.New();

        Assert.NotEqual(id1, id2);
        Assert.NotEqual(Guid.Empty, id1.Value);
    }

    [Fact]
    public void MetricPoint_BasicConstruction()
    {
        var id = MetricId.New();
        var tags = new Dictionary<string, string> { ["host"] = "server1" };
        var timestamp = DateTimeOffset.UtcNow;

        var point = new MetricPoint(
            id,
            MetricNames.ScriptDuration,
            MetricType.Timer,
            1234.5,
            timestamp,
            tags
        );

        Assert.Equal(id, point.Id);
        Assert.Equal(MetricNames.ScriptDuration, point.Name);
        Assert.Equal(MetricType.Timer, point.Type);
        Assert.Equal(1234.5, point.Value);
        Assert.Equal(timestamp, point.Timestamp);
        Assert.Contains("host", point.Tags.Keys);
    }

    [Theory]
    [InlineData(MetricType.Counter)]
    [InlineData(MetricType.Gauge)]
    [InlineData(MetricType.Histogram)]
    [InlineData(MetricType.Timer)]
    public void MetricType_AllValues(MetricType type)
    {
        var point = new MetricPoint(
            MetricId.New(),
            "test",
            type,
            42.0,
            DateTimeOffset.UtcNow,
            new Dictionary<string, string>()
        );

        Assert.Equal(type, point.Type);
    }

    [Fact]
    public void MetricAggregate_Properties()
    {
        var agg = new MetricAggregate(
            "test.metric",
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow,
            TimeSpan.FromHours(1),
            100,
            10.0,
            200.0,
            5000.0,
            50.0,
            45.0,
            90.0,
            99.0,
            new Dictionary<string, string>()
        ) { Variance = 625.0 };

        Assert.Equal("test.metric", agg.Name);
        Assert.Equal(100, agg.Count);
        Assert.Equal(10.0, agg.Min);
        Assert.Equal(200.0, agg.Max);
        Assert.Equal(50.0, agg.Avg);
        Assert.Equal(45.0, agg.P50);
        Assert.Equal(90.0, agg.P90);
        Assert.Equal(99.0, agg.P99);
        Assert.Equal(25.0, agg.StdDev); // sqrt(625)
    }

    [Fact]
    public void MetricAggregate_StdDev_ZeroForSinglePoint()
    {
        var agg = new MetricAggregate(
            "test",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            TimeSpan.Zero,
            1,
            50.0, 50.0, 50.0, 50.0,
            50.0, 50.0, 50.0,
            new Dictionary<string, string>()
        ) { Variance = 0 };

        Assert.Equal(0, agg.StdDev);
    }

    [Fact]
    public void MetricTimeSeries_Construction()
    {
        var points = new List<TimeSeriesPoint>
        {
            new(DateTimeOffset.UtcNow.AddMinutes(-2), 10.0),
            new(DateTimeOffset.UtcNow.AddMinutes(-1), 20.0),
            new(DateTimeOffset.UtcNow, 30.0)
        };

        var series = new MetricTimeSeries(
            "cpu.usage",
            new Dictionary<string, string> { ["host"] = "server1" },
            points
        );

        Assert.Equal("cpu.usage", series.Name);
        Assert.Equal(3, series.Points.Count);
        Assert.Equal("server1", series.Tags["host"]);
    }

    [Fact]
    public void MetricNames_Constants()
    {
        Assert.Equal("script.duration_ms", MetricNames.ScriptDuration);
        Assert.Equal("script.success", MetricNames.ScriptSuccess);
        Assert.Equal("script.failure", MetricNames.ScriptFailure);
        Assert.Equal("runbook.duration_ms", MetricNames.RunbookDuration);
        Assert.Equal("system.cpu_percent", MetricNames.SystemCpuPercent);
    }

    [Fact]
    public void MetricTags_Constants()
    {
        Assert.Equal("thing_id", MetricTags.ThingId);
        Assert.Equal("runbook_id", MetricTags.RunbookId);
        Assert.Equal("status", MetricTags.Status);
        Assert.Equal("host", MetricTags.Host);
    }

    [Fact]
    public void AlertRule_BasicConstruction()
    {
        var ruleId = AlertRuleId.New();
        var actions = new List<AlertAction>
        {
            new(AlertActionType.Notification, new Dictionary<string, string>())
        };

        var rule = new AlertRule(
            ruleId,
            "High CPU Alert",
            "Alert when CPU exceeds 90%",
            MetricNames.SystemCpuPercent,
            AlertCondition.GreaterThan,
            90.0,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(30),
            AlertSeverity.Warning,
            true,
            new Dictionary<string, string>(),
            actions
        );

        Assert.Equal(ruleId, rule.Id);
        Assert.Equal("High CPU Alert", rule.Name);
        Assert.Equal(AlertCondition.GreaterThan, rule.Condition);
        Assert.Equal(90.0, rule.Threshold);
        Assert.True(rule.IsEnabled);
        Assert.Single(rule.Actions);
    }

    [Theory]
    [InlineData(AlertCondition.GreaterThan)]
    [InlineData(AlertCondition.GreaterThanOrEqual)]
    [InlineData(AlertCondition.LessThan)]
    [InlineData(AlertCondition.LessThanOrEqual)]
    [InlineData(AlertCondition.Equal)]
    [InlineData(AlertCondition.NotEqual)]
    [InlineData(AlertCondition.AbsoluteChange)]
    [InlineData(AlertCondition.PercentChange)]
    [InlineData(AlertCondition.Anomaly)]
    public void AlertCondition_AllValues(AlertCondition condition)
    {
        Assert.True(Enum.IsDefined(condition));
    }

    [Theory]
    [InlineData(AlertSeverity.Info)]
    [InlineData(AlertSeverity.Warning)]
    [InlineData(AlertSeverity.Error)]
    [InlineData(AlertSeverity.Critical)]
    public void AlertSeverity_AllValues(AlertSeverity severity)
    {
        Assert.True(Enum.IsDefined(severity));
    }

    [Fact]
    public void Alert_FiringState()
    {
        var alert = new Alert(
            AlertId.New(),
            AlertRuleId.New(),
            "Test Rule",
            AlertSeverity.Warning,
            "Test alert message",
            95.0,
            90.0,
            DateTimeOffset.UtcNow,
            null,
            AlertStatus.Firing,
            new Dictionary<string, string>()
        );

        Assert.False(alert.IsResolved);
        Assert.NotNull(alert.Duration);
        Assert.Equal(AlertStatus.Firing, alert.Status);
    }

    [Fact]
    public void Alert_ResolvedState()
    {
        var firedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var resolvedAt = DateTimeOffset.UtcNow;

        var alert = new Alert(
            AlertId.New(),
            AlertRuleId.New(),
            "Test Rule",
            AlertSeverity.Warning,
            "Test alert message",
            85.0,
            90.0,
            firedAt,
            resolvedAt,
            AlertStatus.Resolved,
            new Dictionary<string, string>()
        );

        Assert.True(alert.IsResolved);
        Assert.NotNull(alert.Duration);
        Assert.InRange(alert.Duration!.Value.TotalMinutes, 9.9, 10.1);
    }

    [Fact]
    public void AlertAction_Construction()
    {
        var config = new Dictionary<string, string>
        {
            ["url"] = "https://webhook.example.com",
            ["secret"] = "abc123"
        };

        var action = new AlertAction(AlertActionType.Webhook, config);

        Assert.Equal(AlertActionType.Webhook, action.Type);
        Assert.Equal("https://webhook.example.com", action.Config["url"]);
    }

    [Theory]
    [InlineData(AlertActionType.Notification)]
    [InlineData(AlertActionType.Email)]
    [InlineData(AlertActionType.Webhook)]
    [InlineData(AlertActionType.RunRunbook)]
    [InlineData(AlertActionType.Script)]
    public void AlertActionType_AllValues(AlertActionType type)
    {
        var action = new AlertAction(type, new Dictionary<string, string>());
        Assert.Equal(type, action.Type);
    }

    [Fact]
    public void HealthCheck_Construction()
    {
        var check = new HealthCheck(
            HealthCheckId.New(),
            "API Health",
            "Check if API is responding",
            HealthCheckType.Http,
            new Dictionary<string, string> { ["url"] = "https://api.example.com/health" },
            TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(30),
            true
        );

        Assert.Equal("API Health", check.Name);
        Assert.Equal(HealthCheckType.Http, check.Type);
        Assert.True(check.IsEnabled);
    }

    [Theory]
    [InlineData(HealthCheckType.Http)]
    [InlineData(HealthCheckType.Tcp)]
    [InlineData(HealthCheckType.Dns)]
    [InlineData(HealthCheckType.Ping)]
    [InlineData(HealthCheckType.Script)]
    [InlineData(HealthCheckType.Database)]
    [InlineData(HealthCheckType.Service)]
    public void HealthCheckType_AllValues(HealthCheckType type)
    {
        Assert.True(Enum.IsDefined(type));
    }

    [Fact]
    public void HealthCheckResult_Healthy()
    {
        var result = new HealthCheckResult(
            HealthCheckId.New(),
            "API Health",
            HealthStatus.Healthy,
            DateTimeOffset.UtcNow,
            TimeSpan.FromMilliseconds(150),
            "OK",
            new Dictionary<string, object> { ["status_code"] = 200 }
        );

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(150, result.ResponseTime.TotalMilliseconds);
    }

    [Theory]
    [InlineData(HealthStatus.Healthy)]
    [InlineData(HealthStatus.Degraded)]
    [InlineData(HealthStatus.Unhealthy)]
    [InlineData(HealthStatus.Unknown)]
    public void HealthStatus_AllValues(HealthStatus status)
    {
        Assert.True(Enum.IsDefined(status));
    }

    [Fact]
    public void SelfHealingRule_Construction()
    {
        var rule = new SelfHealingRule(
            SelfHealingRuleId.New(),
            "Restart on High Memory",
            "Automatically restart service when memory exceeds threshold",
            "alert.metric == 'memory.percent' && alert.severity >= Warning",
            RunbookId.New(),
            3,
            TimeSpan.FromMinutes(15),
            false,
            true
        );

        Assert.Equal("Restart on High Memory", rule.Name);
        Assert.Equal(3, rule.MaxExecutionsPerHour);
        Assert.False(rule.RequiresApproval);
        Assert.True(rule.IsEnabled);
    }

    [Fact]
    public void SelfHealingExecution_Construction()
    {
        var execution = new SelfHealingExecution(
            SelfHealingExecutionId.New(),
            SelfHealingRuleId.New(),
            AlertId.New(),
            RunbookExecutionId.New(),
            SelfHealingStatus.Succeeded,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow,
            "Service restarted successfully"
        );

        Assert.Equal(SelfHealingStatus.Succeeded, execution.Status);
        Assert.NotNull(execution.CompletedAt);
    }

    [Theory]
    [InlineData(SelfHealingStatus.Pending)]
    [InlineData(SelfHealingStatus.AwaitingApproval)]
    [InlineData(SelfHealingStatus.Running)]
    [InlineData(SelfHealingStatus.Succeeded)]
    [InlineData(SelfHealingStatus.Failed)]
    [InlineData(SelfHealingStatus.Skipped)]
    public void SelfHealingStatus_AllValues(SelfHealingStatus status)
    {
        Assert.True(Enum.IsDefined(status));
    }
}
