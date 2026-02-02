using System.Text.Json;
using ControlRoom.Domain.Model;

namespace ControlRoom.Infrastructure.Storage.Queries;

/// <summary>
/// Query object for metrics and observability data
/// </summary>
public sealed class MetricsQueries
{
    private readonly Db _db;

    public MetricsQueries(Db db) => _db = db;

    #region Metrics

    /// <summary>
    /// Record a metric data point
    /// </summary>
    public void RecordMetric(string name, MetricType type, double value, IReadOnlyDictionary<string, string>? tags = null)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO metrics(name, type, value, timestamp, tags_json)
            VALUES ($name, $type, $value, $timestamp, $tags)
            """;
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$type", (int)type);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.Parameters.AddWithValue("$timestamp", DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$tags", JsonSerializer.Serialize(tags ?? new Dictionary<string, string>()));
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Record multiple metric points in a batch
    /// </summary>
    public void RecordMetrics(IEnumerable<(string Name, MetricType Type, double Value, Dictionary<string, string>? Tags)> metrics)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        var timestamp = DateTimeOffset.UtcNow.ToString("O");

        foreach (var m in metrics)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO metrics(name, type, value, timestamp, tags_json)
                VALUES ($name, $type, $value, $timestamp, $tags)
                """;
            cmd.Parameters.AddWithValue("$name", m.Name);
            cmd.Parameters.AddWithValue("$type", (int)m.Type);
            cmd.Parameters.AddWithValue("$value", m.Value);
            cmd.Parameters.AddWithValue("$timestamp", timestamp);
            cmd.Parameters.AddWithValue("$tags", JsonSerializer.Serialize(m.Tags ?? new Dictionary<string, string>()));
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    /// <summary>
    /// Query metrics by name and time range
    /// </summary>
    public IReadOnlyList<MetricPoint> QueryMetrics(
        string name,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        IReadOnlyDictionary<string, string>? tags = null,
        int limit = 1000)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();

        var conditions = new List<string> { "name = $name" };
        cmd.Parameters.AddWithValue("$name", name);

        if (from.HasValue)
        {
            conditions.Add("timestamp >= $from");
            cmd.Parameters.AddWithValue("$from", from.Value.ToString("O"));
        }

        if (to.HasValue)
        {
            conditions.Add("timestamp <= $to");
            cmd.Parameters.AddWithValue("$to", to.Value.ToString("O"));
        }

        cmd.CommandText = $"""
            SELECT id, name, type, value, timestamp, tags_json
            FROM metrics
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY timestamp DESC
            LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$limit", limit);

        var results = new List<MetricPoint>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var tagsJson = reader.GetString(5);
            var metricTags = JsonSerializer.Deserialize<Dictionary<string, string>>(tagsJson) ?? new();

            // Filter by tags if specified
            if (tags != null && tags.Count > 0)
            {
                var match = tags.All(t => metricTags.TryGetValue(t.Key, out var v) && v == t.Value);
                if (!match) continue;
            }

            results.Add(new MetricPoint(
                new MetricId(Guid.NewGuid()), // ID not stored, generate placeholder
                reader.GetString(1),
                (MetricType)reader.GetInt32(2),
                reader.GetDouble(3),
                DateTimeOffset.Parse(reader.GetString(4)),
                metricTags
            ));
        }

        return results;
    }

    /// <summary>
    /// Get aggregated metrics for a time range
    /// </summary>
    public MetricAggregate? GetAggregate(
        string name,
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyDictionary<string, string>? tags = null)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT
                COUNT(*) as count,
                MIN(value) as min,
                MAX(value) as max,
                SUM(value) as sum,
                AVG(value) as avg
            FROM metrics
            WHERE name = $name AND timestamp >= $from AND timestamp <= $to
            """;
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$from", from.ToString("O"));
        cmd.Parameters.AddWithValue("$to", to.ToString("O"));

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        var count = reader.GetInt32(0);
        if (count == 0) return null;

        // Get percentiles (simple approximation using ordered values)
        var values = QueryMetrics(name, from, to, tags, 10000)
            .Select(m => m.Value)
            .OrderBy(v => v)
            .ToList();

        double Percentile(List<double> sorted, double p)
        {
            if (sorted.Count == 0) return 0;
            var index = (int)Math.Ceiling(p / 100.0 * sorted.Count) - 1;
            return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
        }

        return new MetricAggregate(
            name,
            from,
            to,
            to - from,
            count,
            reader.GetDouble(1),
            reader.GetDouble(2),
            reader.GetDouble(3),
            reader.GetDouble(4),
            Percentile(values, 50),
            Percentile(values, 90),
            Percentile(values, 99),
            tags ?? new Dictionary<string, string>()
        );
    }

    /// <summary>
    /// Get time series data for charting
    /// </summary>
    public MetricTimeSeries GetTimeSeries(
        string name,
        DateTimeOffset from,
        DateTimeOffset to,
        TimeSpan bucketSize,
        IReadOnlyDictionary<string, string>? tags = null)
    {
        var metrics = QueryMetrics(name, from, to, tags, 100000);

        var points = metrics
            .GroupBy(m => new DateTimeOffset(
                m.Timestamp.Ticks / bucketSize.Ticks * bucketSize.Ticks,
                m.Timestamp.Offset))
            .Select(g => new TimeSeriesPoint(g.Key, g.Average(m => m.Value)))
            .OrderBy(p => p.Timestamp)
            .ToList();

        return new MetricTimeSeries(name, tags ?? new Dictionary<string, string>(), points);
    }

    /// <summary>
    /// Get the latest value for a metric
    /// </summary>
    public double? GetLatestValue(string name, IReadOnlyDictionary<string, string>? tags = null)
    {
        var metrics = QueryMetrics(name, tags: tags, limit: 1);
        return metrics.Count > 0 ? metrics[0].Value : null;
    }

    /// <summary>
    /// Clean up old metrics (retention policy)
    /// </summary>
    public int PurgeOldMetrics(TimeSpan olderThan)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        var cutoff = DateTimeOffset.UtcNow - olderThan;
        cmd.CommandText = "DELETE FROM metrics WHERE timestamp < $cutoff";
        cmd.Parameters.AddWithValue("$cutoff", cutoff.ToString("O"));
        return cmd.ExecuteNonQuery();
    }

    #endregion

    #region Alerts

    /// <summary>
    /// Create an alert rule
    /// </summary>
    public void CreateAlertRule(AlertRule rule)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO alert_rules(rule_id, name, description, metric_name, condition, threshold,
                                    evaluation_window_ms, cooldown_ms, severity, is_enabled,
                                    tags_json, actions_json, created_at, updated_at)
            VALUES ($id, $name, $desc, $metric, $cond, $thresh, $window, $cooldown, $sev, $enabled,
                    $tags, $actions, $created, $updated)
            """;
        cmd.Parameters.AddWithValue("$id", rule.Id.ToString());
        cmd.Parameters.AddWithValue("$name", rule.Name);
        cmd.Parameters.AddWithValue("$desc", rule.Description);
        cmd.Parameters.AddWithValue("$metric", rule.MetricName);
        cmd.Parameters.AddWithValue("$cond", (int)rule.Condition);
        cmd.Parameters.AddWithValue("$thresh", rule.Threshold);
        cmd.Parameters.AddWithValue("$window", (int)rule.EvaluationWindow.TotalMilliseconds);
        cmd.Parameters.AddWithValue("$cooldown", (int)rule.CooldownPeriod.TotalMilliseconds);
        cmd.Parameters.AddWithValue("$sev", (int)rule.Severity);
        cmd.Parameters.AddWithValue("$enabled", rule.IsEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("$tags", JsonSerializer.Serialize(rule.Tags));
        cmd.Parameters.AddWithValue("$actions", JsonSerializer.Serialize(rule.Actions));
        var now = DateTimeOffset.UtcNow.ToString("O");
        cmd.Parameters.AddWithValue("$created", now);
        cmd.Parameters.AddWithValue("$updated", now);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Get all enabled alert rules
    /// </summary>
    public IReadOnlyList<AlertRule> GetEnabledAlertRules()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT rule_id, name, description, metric_name, condition, threshold,
                   evaluation_window_ms, cooldown_ms, severity, is_enabled, tags_json, actions_json
            FROM alert_rules
            WHERE is_enabled = 1
            """;

        var rules = new List<AlertRule>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rules.Add(new AlertRule(
                new AlertRuleId(Guid.Parse(reader.GetString(0))),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                (AlertCondition)reader.GetInt32(4),
                reader.GetDouble(5),
                TimeSpan.FromMilliseconds(reader.GetInt32(6)),
                TimeSpan.FromMilliseconds(reader.GetInt32(7)),
                (AlertSeverity)reader.GetInt32(8),
                reader.GetInt32(9) == 1,
                JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(10)) ?? new(),
                JsonSerializer.Deserialize<List<AlertAction>>(reader.GetString(11)) ?? new()
            ));
        }
        return rules;
    }

    /// <summary>
    /// Fire an alert
    /// </summary>
    public void FireAlert(Alert alert)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO alerts(alert_id, rule_id, rule_name, severity, message, current_value,
                               threshold, fired_at, resolved_at, status, tags_json)
            VALUES ($id, $rule_id, $rule_name, $sev, $msg, $value, $thresh, $fired, $resolved, $status, $tags)
            """;
        cmd.Parameters.AddWithValue("$id", alert.Id.ToString());
        cmd.Parameters.AddWithValue("$rule_id", alert.RuleId.ToString());
        cmd.Parameters.AddWithValue("$rule_name", alert.RuleName);
        cmd.Parameters.AddWithValue("$sev", (int)alert.Severity);
        cmd.Parameters.AddWithValue("$msg", alert.Message);
        cmd.Parameters.AddWithValue("$value", alert.CurrentValue);
        cmd.Parameters.AddWithValue("$thresh", alert.Threshold);
        cmd.Parameters.AddWithValue("$fired", alert.FiredAt.ToString("O"));
        cmd.Parameters.AddWithValue("$resolved", alert.ResolvedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$status", (int)alert.Status);
        cmd.Parameters.AddWithValue("$tags", JsonSerializer.Serialize(alert.Tags));
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Get active (firing) alerts
    /// </summary>
    public IReadOnlyList<Alert> GetActiveAlerts()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT alert_id, rule_id, rule_name, severity, message, current_value,
                   threshold, fired_at, resolved_at, status, tags_json
            FROM alerts
            WHERE status = 0
            ORDER BY severity DESC, fired_at DESC
            """;

        var alerts = new List<Alert>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            alerts.Add(ParseAlert(reader));
        }
        return alerts;
    }

    /// <summary>
    /// Get recent alerts
    /// </summary>
    public IReadOnlyList<Alert> GetRecentAlerts(int limit = 100)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT alert_id, rule_id, rule_name, severity, message, current_value,
                   threshold, fired_at, resolved_at, status, tags_json
            FROM alerts
            ORDER BY fired_at DESC
            LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$limit", limit);

        var alerts = new List<Alert>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            alerts.Add(ParseAlert(reader));
        }
        return alerts;
    }

    /// <summary>
    /// Resolve an alert
    /// </summary>
    public void ResolveAlert(AlertId alertId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE alerts
            SET status = $status, resolved_at = $resolved
            WHERE alert_id = $id
            """;
        cmd.Parameters.AddWithValue("$id", alertId.ToString());
        cmd.Parameters.AddWithValue("$status", (int)AlertStatus.Resolved);
        cmd.Parameters.AddWithValue("$resolved", DateTimeOffset.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Acknowledge an alert
    /// </summary>
    public void AcknowledgeAlert(AlertId alertId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE alerts
            SET status = $status
            WHERE alert_id = $id
            """;
        cmd.Parameters.AddWithValue("$id", alertId.ToString());
        cmd.Parameters.AddWithValue("$status", (int)AlertStatus.Acknowledged);
        cmd.ExecuteNonQuery();
    }

    private Alert ParseAlert(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        return new Alert(
            new AlertId(Guid.Parse(reader.GetString(0))),
            new AlertRuleId(Guid.Parse(reader.GetString(1))),
            reader.GetString(2),
            (AlertSeverity)reader.GetInt32(3),
            reader.GetString(4),
            reader.GetDouble(5),
            reader.GetDouble(6),
            DateTimeOffset.Parse(reader.GetString(7)),
            reader.IsDBNull(8) ? null : DateTimeOffset.Parse(reader.GetString(8)),
            (AlertStatus)reader.GetInt32(9),
            JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(10)) ?? new()
        );
    }

    #endregion

    #region Health Checks

    /// <summary>
    /// Create a health check
    /// </summary>
    public void CreateHealthCheck(HealthCheck check)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO health_checks(check_id, name, description, type, config_json,
                                      interval_ms, timeout_ms, is_enabled, created_at, updated_at)
            VALUES ($id, $name, $desc, $type, $config, $interval, $timeout, $enabled, $created, $updated)
            """;
        cmd.Parameters.AddWithValue("$id", check.Id.ToString());
        cmd.Parameters.AddWithValue("$name", check.Name);
        cmd.Parameters.AddWithValue("$desc", check.Description);
        cmd.Parameters.AddWithValue("$type", (int)check.Type);
        cmd.Parameters.AddWithValue("$config", JsonSerializer.Serialize(check.Config));
        cmd.Parameters.AddWithValue("$interval", (int)check.Interval.TotalMilliseconds);
        cmd.Parameters.AddWithValue("$timeout", (int)check.Timeout.TotalMilliseconds);
        cmd.Parameters.AddWithValue("$enabled", check.IsEnabled ? 1 : 0);
        var now = DateTimeOffset.UtcNow.ToString("O");
        cmd.Parameters.AddWithValue("$created", now);
        cmd.Parameters.AddWithValue("$updated", now);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Get all enabled health checks
    /// </summary>
    public IReadOnlyList<HealthCheck> GetEnabledHealthChecks()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT check_id, name, description, type, config_json, interval_ms, timeout_ms, is_enabled
            FROM health_checks
            WHERE is_enabled = 1
            """;

        var checks = new List<HealthCheck>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            checks.Add(new HealthCheck(
                new HealthCheckId(Guid.Parse(reader.GetString(0))),
                reader.GetString(1),
                reader.GetString(2),
                (HealthCheckType)reader.GetInt32(3),
                JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(4)) ?? new(),
                TimeSpan.FromMilliseconds(reader.GetInt32(5)),
                TimeSpan.FromMilliseconds(reader.GetInt32(6)),
                reader.GetInt32(7) == 1
            ));
        }
        return checks;
    }

    /// <summary>
    /// Record a health check result
    /// </summary>
    public void RecordHealthCheckResult(HealthCheckResult result)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO health_check_results(check_id, check_name, status, checked_at,
                                             response_time_ms, message, details_json)
            VALUES ($id, $name, $status, $checked, $response, $msg, $details)
            """;
        cmd.Parameters.AddWithValue("$id", result.CheckId.ToString());
        cmd.Parameters.AddWithValue("$name", result.CheckName);
        cmd.Parameters.AddWithValue("$status", (int)result.Status);
        cmd.Parameters.AddWithValue("$checked", result.CheckedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$response", (int)result.ResponseTime.TotalMilliseconds);
        cmd.Parameters.AddWithValue("$msg", result.Message ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$details", result.Details != null
            ? JsonSerializer.Serialize(result.Details)
            : DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Get latest health check results for all checks
    /// </summary>
    public IReadOnlyList<HealthCheckResult> GetLatestHealthCheckResults()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT r.check_id, r.check_name, r.status, r.checked_at, r.response_time_ms, r.message, r.details_json
            FROM health_check_results r
            INNER JOIN (
                SELECT check_id, MAX(id) as max_id
                FROM health_check_results
                GROUP BY check_id
            ) latest ON r.check_id = latest.check_id AND r.id = latest.max_id
            """;

        var results = new List<HealthCheckResult>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new HealthCheckResult(
                new HealthCheckId(Guid.Parse(reader.GetString(0))),
                reader.GetString(1),
                (HealthStatus)reader.GetInt32(2),
                DateTimeOffset.Parse(reader.GetString(3)),
                TimeSpan.FromMilliseconds(reader.GetInt32(4)),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(reader.GetString(6))
            ));
        }
        return results;
    }

    #endregion

    #region Self-Healing

    /// <summary>
    /// Create a self-healing rule
    /// </summary>
    public void CreateSelfHealingRule(SelfHealingRule rule)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO self_healing_rules(rule_id, name, description, trigger_condition,
                                           remediation_runbook_id, max_executions_per_hour,
                                           cooldown_ms, requires_approval, is_enabled, created_at, updated_at)
            VALUES ($id, $name, $desc, $trigger, $runbook, $max, $cooldown, $approval, $enabled, $created, $updated)
            """;
        cmd.Parameters.AddWithValue("$id", rule.Id.ToString());
        cmd.Parameters.AddWithValue("$name", rule.Name);
        cmd.Parameters.AddWithValue("$desc", rule.Description);
        cmd.Parameters.AddWithValue("$trigger", rule.TriggerCondition);
        cmd.Parameters.AddWithValue("$runbook", rule.RemediationRunbook.ToString());
        cmd.Parameters.AddWithValue("$max", rule.MaxExecutionsPerHour);
        cmd.Parameters.AddWithValue("$cooldown", (int)rule.CooldownPeriod.TotalMilliseconds);
        cmd.Parameters.AddWithValue("$approval", rule.RequiresApproval ? 1 : 0);
        cmd.Parameters.AddWithValue("$enabled", rule.IsEnabled ? 1 : 0);
        var now = DateTimeOffset.UtcNow.ToString("O");
        cmd.Parameters.AddWithValue("$created", now);
        cmd.Parameters.AddWithValue("$updated", now);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Get all enabled self-healing rules
    /// </summary>
    public IReadOnlyList<SelfHealingRule> GetEnabledSelfHealingRules()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT rule_id, name, description, trigger_condition, remediation_runbook_id,
                   max_executions_per_hour, cooldown_ms, requires_approval, is_enabled
            FROM self_healing_rules
            WHERE is_enabled = 1
            """;

        var rules = new List<SelfHealingRule>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rules.Add(new SelfHealingRule(
                new SelfHealingRuleId(Guid.Parse(reader.GetString(0))),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                new RunbookId(Guid.Parse(reader.GetString(4))),
                reader.GetInt32(5),
                TimeSpan.FromMilliseconds(reader.GetInt32(6)),
                reader.GetInt32(7) == 1,
                reader.GetInt32(8) == 1
            ));
        }
        return rules;
    }

    /// <summary>
    /// Record a self-healing execution
    /// </summary>
    public void RecordSelfHealingExecution(SelfHealingExecution execution)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO self_healing_executions(execution_id, rule_id, triggering_alert_id,
                                                remediation_execution_id, status, started_at, completed_at, result)
            VALUES ($id, $rule, $alert, $remediation, $status, $started, $completed, $result)
            """;
        cmd.Parameters.AddWithValue("$id", execution.Id.ToString());
        cmd.Parameters.AddWithValue("$rule", execution.RuleId.ToString());
        cmd.Parameters.AddWithValue("$alert", execution.TriggeringAlert?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$remediation", execution.RemediationExecution?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$status", (int)execution.Status);
        cmd.Parameters.AddWithValue("$started", execution.StartedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$completed", execution.CompletedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$result", execution.Result ?? (object)DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Count self-healing executions in the last hour for rate limiting
    /// </summary>
    public int CountRecentExecutions(SelfHealingRuleId ruleId, TimeSpan window)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        var cutoff = DateTimeOffset.UtcNow - window;
        cmd.CommandText = """
            SELECT COUNT(*)
            FROM self_healing_executions
            WHERE rule_id = $rule AND started_at >= $cutoff
            """;
        cmd.Parameters.AddWithValue("$rule", ruleId.ToString());
        cmd.Parameters.AddWithValue("$cutoff", cutoff.ToString("O"));
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    #endregion
}
