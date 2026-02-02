using System.Text.Json;
using ControlRoom.Domain.Model;

namespace ControlRoom.Infrastructure.Storage.Queries;

/// <summary>
/// Query object for runbooks and executions
/// </summary>
public sealed record RunbookListItem(
    RunbookId RunbookId,
    string Name,
    string Description,
    bool IsEnabled,
    int StepCount,
    TriggerType? TriggerType,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version
);

/// <summary>
/// Execution summary for list views
/// </summary>
public sealed record RunbookExecutionListItem(
    RunbookExecutionId ExecutionId,
    RunbookId RunbookId,
    string RunbookName,
    RunbookExecutionStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    int TotalSteps,
    int CompletedSteps,
    int FailedSteps
);

public sealed class RunbookQueries
{
    private readonly Db _db;

    public RunbookQueries(Db db) => _db = db;

    /// <summary>
    /// List all runbooks
    /// </summary>
    public IReadOnlyList<RunbookListItem> ListRunbooks(bool? enabledOnly = null)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();

        var whereClause = enabledOnly.HasValue
            ? $"WHERE is_enabled = {(enabledOnly.Value ? 1 : 0)}"
            : "";

        cmd.CommandText = $"""
            SELECT runbook_id, name, description, is_enabled, config_json,
                   created_at, updated_at, version
            FROM runbooks
            {whereClause}
            ORDER BY updated_at DESC
            """;

        var list = new List<RunbookListItem>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var configJson = r.GetString(4);
            var config = RunbookConfig.Parse(configJson);
            var triggerType = config.Trigger?.TriggerType;

            list.Add(new RunbookListItem(
                new RunbookId(Guid.Parse(r.GetString(0))),
                r.GetString(1),
                r.GetString(2),
                r.GetInt32(3) == 1,
                config.Steps.Count,
                triggerType,
                DateTimeOffset.Parse(r.GetString(5)),
                DateTimeOffset.Parse(r.GetString(6)),
                r.GetInt32(7)
            ));
        }
        return list;
    }

    /// <summary>
    /// Get a runbook by ID
    /// </summary>
    public Runbook? GetRunbook(RunbookId runbookId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT runbook_id, name, description, config_json, is_enabled,
                   created_at, updated_at, version
            FROM runbooks
            WHERE runbook_id = $id
            """;
        cmd.Parameters.AddWithValue("$id", runbookId.ToString());

        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;

        var config = RunbookConfig.Parse(r.GetString(3));
        var steps = config.Steps.Select(s => s.ToStep()).ToList();

        return new Runbook(
            new RunbookId(Guid.Parse(r.GetString(0))),
            r.GetString(1),
            r.GetString(2),
            steps,
            config.Trigger,
            r.GetInt32(4) == 1,
            DateTimeOffset.Parse(r.GetString(5)),
            DateTimeOffset.Parse(r.GetString(6))
        );
    }

    /// <summary>
    /// Get a runbook by name
    /// </summary>
    public Runbook? GetRunbookByName(string name)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT runbook_id
            FROM runbooks
            WHERE name = $name
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$name", name);

        var result = cmd.ExecuteScalar();
        if (result is string idStr)
        {
            return GetRunbook(new RunbookId(Guid.Parse(idStr)));
        }
        return null;
    }

    /// <summary>
    /// Insert a new runbook
    /// </summary>
    public void InsertRunbook(Runbook runbook)
    {
        var config = new RunbookConfig
        {
            Name = runbook.Name,
            Description = runbook.Description,
            Steps = runbook.Steps.Select(s => new RunbookStepConfig
            {
                StepId = s.StepId,
                Name = s.Name,
                ThingId = s.ThingId.ToString(),
                ProfileId = s.ProfileId,
                ConditionType = s.Condition.Type,
                ConditionExpression = s.Condition.Expression,
                DependsOn = s.DependsOn.ToList(),
                Retry = s.Retry is null ? null : new RetryPolicyConfig
                {
                    MaxAttempts = s.Retry.MaxAttempts,
                    InitialDelaySeconds = (int)s.Retry.InitialDelay.TotalSeconds,
                    BackoffMultiplier = s.Retry.BackoffMultiplier,
                    MaxDelaySeconds = (int)s.Retry.MaxDelay.TotalSeconds
                },
                TimeoutSeconds = s.Timeout.HasValue ? (int)s.Timeout.Value.TotalSeconds : null,
                ArgumentsOverride = s.ArgumentsOverride
            }).ToList(),
            Trigger = runbook.Trigger,
            IsEnabled = runbook.IsEnabled
        };

        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO runbooks(runbook_id, name, description, config_json, is_enabled,
                                 created_at, updated_at, version)
            VALUES ($id, $name, $desc, $cfg, $enabled, $created, $updated, 1)
            """;
        cmd.Parameters.AddWithValue("$id", runbook.Id.ToString());
        cmd.Parameters.AddWithValue("$name", runbook.Name);
        cmd.Parameters.AddWithValue("$desc", runbook.Description);
        cmd.Parameters.AddWithValue("$cfg", config.ToJson());
        cmd.Parameters.AddWithValue("$enabled", runbook.IsEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("$created", runbook.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$updated", runbook.UpdatedAt.ToString("O"));
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    /// <summary>
    /// Update an existing runbook
    /// </summary>
    public void UpdateRunbook(Runbook runbook)
    {
        var config = new RunbookConfig
        {
            Name = runbook.Name,
            Description = runbook.Description,
            Steps = runbook.Steps.Select(s => new RunbookStepConfig
            {
                StepId = s.StepId,
                Name = s.Name,
                ThingId = s.ThingId.ToString(),
                ProfileId = s.ProfileId,
                ConditionType = s.Condition.Type,
                ConditionExpression = s.Condition.Expression,
                DependsOn = s.DependsOn.ToList(),
                Retry = s.Retry is null ? null : new RetryPolicyConfig
                {
                    MaxAttempts = s.Retry.MaxAttempts,
                    InitialDelaySeconds = (int)s.Retry.InitialDelay.TotalSeconds,
                    BackoffMultiplier = s.Retry.BackoffMultiplier,
                    MaxDelaySeconds = (int)s.Retry.MaxDelay.TotalSeconds
                },
                TimeoutSeconds = s.Timeout.HasValue ? (int)s.Timeout.Value.TotalSeconds : null,
                ArgumentsOverride = s.ArgumentsOverride
            }).ToList(),
            Trigger = runbook.Trigger,
            IsEnabled = runbook.IsEnabled
        };

        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            UPDATE runbooks
            SET name = $name, description = $desc, config_json = $cfg,
                is_enabled = $enabled, updated_at = $updated, version = version + 1
            WHERE runbook_id = $id
            """;
        cmd.Parameters.AddWithValue("$id", runbook.Id.ToString());
        cmd.Parameters.AddWithValue("$name", runbook.Name);
        cmd.Parameters.AddWithValue("$desc", runbook.Description);
        cmd.Parameters.AddWithValue("$cfg", config.ToJson());
        cmd.Parameters.AddWithValue("$enabled", runbook.IsEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("$updated", runbook.UpdatedAt.ToString("O"));
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    /// <summary>
    /// Delete a runbook
    /// </summary>
    public void DeleteRunbook(RunbookId runbookId)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();

        // Delete related data first (respecting foreign keys)
        using var delTriggers = conn.CreateCommand();
        delTriggers.Transaction = tx;
        delTriggers.CommandText = "DELETE FROM trigger_history WHERE runbook_id = $id";
        delTriggers.Parameters.AddWithValue("$id", runbookId.ToString());
        delTriggers.ExecuteNonQuery();

        // Delete step executions through execution IDs
        using var delSteps = conn.CreateCommand();
        delSteps.Transaction = tx;
        delSteps.CommandText = """
            DELETE FROM step_executions
            WHERE execution_id IN (
                SELECT execution_id FROM runbook_executions WHERE runbook_id = $id
            )
            """;
        delSteps.Parameters.AddWithValue("$id", runbookId.ToString());
        delSteps.ExecuteNonQuery();

        using var delExecs = conn.CreateCommand();
        delExecs.Transaction = tx;
        delExecs.CommandText = "DELETE FROM runbook_executions WHERE runbook_id = $id";
        delExecs.Parameters.AddWithValue("$id", runbookId.ToString());
        delExecs.ExecuteNonQuery();

        using var delRunbook = conn.CreateCommand();
        delRunbook.Transaction = tx;
        delRunbook.CommandText = "DELETE FROM runbooks WHERE runbook_id = $id";
        delRunbook.Parameters.AddWithValue("$id", runbookId.ToString());
        delRunbook.ExecuteNonQuery();

        tx.Commit();
    }

    /// <summary>
    /// List executions for a runbook
    /// </summary>
    public IReadOnlyList<RunbookExecutionListItem> ListExecutions(
        RunbookId? runbookId = null,
        int limit = 100)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();

        var whereClause = runbookId.HasValue
            ? "WHERE e.runbook_id = $runbook_id"
            : "";

        cmd.CommandText = $"""
            SELECT e.execution_id, e.runbook_id, r.name, e.status, e.started_at, e.ended_at,
                   (SELECT COUNT(*) FROM step_executions WHERE execution_id = e.execution_id) as total,
                   (SELECT COUNT(*) FROM step_executions
                    WHERE execution_id = e.execution_id AND status = 4) as completed,
                   (SELECT COUNT(*) FROM step_executions
                    WHERE execution_id = e.execution_id AND status = 5) as failed
            FROM runbook_executions e
            JOIN runbooks r ON r.runbook_id = e.runbook_id
            {whereClause}
            ORDER BY e.started_at DESC
            LIMIT $limit
            """;

        if (runbookId.HasValue)
            cmd.Parameters.AddWithValue("$runbook_id", runbookId.Value.ToString());
        cmd.Parameters.AddWithValue("$limit", limit);

        var list = new List<RunbookExecutionListItem>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new RunbookExecutionListItem(
                new RunbookExecutionId(Guid.Parse(reader.GetString(0))),
                new RunbookId(Guid.Parse(reader.GetString(1))),
                reader.GetString(2),
                (RunbookExecutionStatus)reader.GetInt32(3),
                DateTimeOffset.Parse(reader.GetString(4)),
                reader.IsDBNull(5) ? null : DateTimeOffset.Parse(reader.GetString(5)),
                reader.GetInt32(6),
                reader.GetInt32(7),
                reader.GetInt32(8)
            ));
        }
        return list;
    }

    /// <summary>
    /// Get a specific execution with all step details
    /// </summary>
    public RunbookExecution? GetExecution(RunbookExecutionId executionId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT execution_id, runbook_id, status, started_at, ended_at,
                   trigger_info, error_message
            FROM runbook_executions
            WHERE execution_id = $id
            """;
        cmd.Parameters.AddWithValue("$id", executionId.ToString());

        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;

        var execId = new RunbookExecutionId(Guid.Parse(r.GetString(0)));
        var runbookId = new RunbookId(Guid.Parse(r.GetString(1)));
        var status = (RunbookExecutionStatus)r.GetInt32(2);
        var startedAt = DateTimeOffset.Parse(r.GetString(3));
        var endedAt = r.IsDBNull(4) ? (DateTimeOffset?)null : DateTimeOffset.Parse(r.GetString(4));
        var triggerInfo = r.IsDBNull(5) ? null : r.GetString(5);
        var errorMessage = r.IsDBNull(6) ? null : r.GetString(6);

        r.Close();

        // Get step executions
        var steps = GetStepExecutions(execId);

        return new RunbookExecution(
            execId,
            runbookId,
            status,
            startedAt,
            endedAt,
            steps,
            triggerInfo,
            errorMessage
        );
    }

    /// <summary>
    /// Get step executions for a runbook execution
    /// </summary>
    public IReadOnlyList<StepExecution> GetStepExecutions(RunbookExecutionId executionId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT step_id, step_name, run_id, status, started_at, ended_at,
                   attempt, error_message, output
            FROM step_executions
            WHERE execution_id = $id
            ORDER BY id ASC
            """;
        cmd.Parameters.AddWithValue("$id", executionId.ToString());

        var list = new List<StepExecution>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new StepExecution(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : new RunId(Guid.Parse(reader.GetString(2))),
                (StepExecutionStatus)reader.GetInt32(3),
                reader.IsDBNull(4) ? null : DateTimeOffset.Parse(reader.GetString(4)),
                reader.IsDBNull(5) ? null : DateTimeOffset.Parse(reader.GetString(5)),
                reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8)
            ));
        }
        return list;
    }

    /// <summary>
    /// Insert a new execution
    /// </summary>
    public void InsertExecution(RunbookExecution execution)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO runbook_executions(execution_id, runbook_id, status, started_at,
                                           ended_at, trigger_info, error_message)
            VALUES ($id, $runbook_id, $status, $started, $ended, $trigger, $error)
            """;
        cmd.Parameters.AddWithValue("$id", execution.Id.ToString());
        cmd.Parameters.AddWithValue("$runbook_id", execution.RunbookId.ToString());
        cmd.Parameters.AddWithValue("$status", (int)execution.Status);
        cmd.Parameters.AddWithValue("$started", execution.StartedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$ended", execution.EndedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$trigger", execution.TriggerInfo ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$error", execution.ErrorMessage ?? (object)DBNull.Value);
        cmd.ExecuteNonQuery();

        // Insert step executions
        foreach (var step in execution.StepExecutions)
        {
            using var stepCmd = conn.CreateCommand();
            stepCmd.Transaction = tx;
            stepCmd.CommandText = """
                INSERT INTO step_executions(execution_id, step_id, step_name, run_id, status,
                                            started_at, ended_at, attempt, error_message, output)
                VALUES ($exec_id, $step_id, $step_name, $run_id, $status,
                        $started, $ended, $attempt, $error, $output)
                """;
            stepCmd.Parameters.AddWithValue("$exec_id", execution.Id.ToString());
            stepCmd.Parameters.AddWithValue("$step_id", step.StepId);
            stepCmd.Parameters.AddWithValue("$step_name", step.StepName);
            stepCmd.Parameters.AddWithValue("$run_id", step.RunId?.ToString() ?? (object)DBNull.Value);
            stepCmd.Parameters.AddWithValue("$status", (int)step.Status);
            stepCmd.Parameters.AddWithValue("$started", step.StartedAt?.ToString("O") ?? (object)DBNull.Value);
            stepCmd.Parameters.AddWithValue("$ended", step.EndedAt?.ToString("O") ?? (object)DBNull.Value);
            stepCmd.Parameters.AddWithValue("$attempt", step.Attempt);
            stepCmd.Parameters.AddWithValue("$error", step.ErrorMessage ?? (object)DBNull.Value);
            stepCmd.Parameters.AddWithValue("$output", step.Output ?? (object)DBNull.Value);
            stepCmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    /// <summary>
    /// Update execution status
    /// </summary>
    public void UpdateExecutionStatus(
        RunbookExecutionId executionId,
        RunbookExecutionStatus status,
        DateTimeOffset? endedAt = null,
        string? errorMessage = null)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE runbook_executions
            SET status = $status, ended_at = $ended, error_message = $error
            WHERE execution_id = $id
            """;
        cmd.Parameters.AddWithValue("$id", executionId.ToString());
        cmd.Parameters.AddWithValue("$status", (int)status);
        cmd.Parameters.AddWithValue("$ended", endedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$error", errorMessage ?? (object)DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Update step execution
    /// </summary>
    public void UpdateStepExecution(
        RunbookExecutionId executionId,
        string stepId,
        StepExecutionStatus status,
        RunId? runId = null,
        DateTimeOffset? startedAt = null,
        DateTimeOffset? endedAt = null,
        int? attempt = null,
        string? errorMessage = null,
        string? output = null)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();

        var updates = new List<string> { "status = $status" };
        cmd.Parameters.AddWithValue("$status", (int)status);

        if (runId.HasValue)
        {
            updates.Add("run_id = $run_id");
            cmd.Parameters.AddWithValue("$run_id", runId.Value.ToString());
        }
        if (startedAt.HasValue)
        {
            updates.Add("started_at = $started");
            cmd.Parameters.AddWithValue("$started", startedAt.Value.ToString("O"));
        }
        if (endedAt.HasValue)
        {
            updates.Add("ended_at = $ended");
            cmd.Parameters.AddWithValue("$ended", endedAt.Value.ToString("O"));
        }
        if (attempt.HasValue)
        {
            updates.Add("attempt = $attempt");
            cmd.Parameters.AddWithValue("$attempt", attempt.Value);
        }
        if (errorMessage != null)
        {
            updates.Add("error_message = $error");
            cmd.Parameters.AddWithValue("$error", errorMessage);
        }
        if (output != null)
        {
            updates.Add("output = $output");
            cmd.Parameters.AddWithValue("$output", output);
        }

        cmd.CommandText = $"""
            UPDATE step_executions
            SET {string.Join(", ", updates)}
            WHERE execution_id = $exec_id AND step_id = $step_id
            """;
        cmd.Parameters.AddWithValue("$exec_id", executionId.ToString());
        cmd.Parameters.AddWithValue("$step_id", stepId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Get runbooks that have schedule triggers
    /// </summary>
    public IReadOnlyList<Runbook> GetScheduledRunbooks()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT runbook_id
            FROM runbooks
            WHERE is_enabled = 1
              AND json_extract(config_json, '$.Trigger.type') = 'schedule'
            """;

        var list = new List<Runbook>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var runbook = GetRunbook(new RunbookId(Guid.Parse(reader.GetString(0))));
            if (runbook != null)
                list.Add(runbook);
        }
        return list;
    }

    /// <summary>
    /// Record trigger history
    /// </summary>
    public void RecordTrigger(
        RunbookId runbookId,
        TriggerType triggerType,
        RunbookExecutionId? executionId = null,
        string? payloadJson = null)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO trigger_history(runbook_id, trigger_type, fired_at, execution_id, payload_json)
            VALUES ($runbook_id, $type, $at, $exec_id, $payload)
            """;
        cmd.Parameters.AddWithValue("$runbook_id", runbookId.ToString());
        cmd.Parameters.AddWithValue("$type", (int)triggerType);
        cmd.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$exec_id", executionId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$payload", payloadJson ?? (object)DBNull.Value);
        cmd.ExecuteNonQuery();
    }
}
