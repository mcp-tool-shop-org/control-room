using System.Text.Json;
using Microsoft.Data.Sqlite;
using ControlRoom.Domain.Model;
using ControlRoom.Infrastructure.Storage;
using ControlRoom.Infrastructure.Storage.Queries;

namespace ControlRoom.Application.UseCases;

/// <summary>
/// Use case for activity feed and audit logging.
/// </summary>
public sealed class ActivityFeed
{
    private readonly Db _db;
    private readonly TeamQueries _queries;
    private readonly TeamManagement _teamManagement;

    public ActivityFeed(Db db, TeamManagement teamManagement)
    {
        _db = db;
        _queries = new TeamQueries(db);
        _teamManagement = teamManagement;
    }

    // ========================================================================
    // Activity Feed Operations
    // ========================================================================

    /// <summary>
    /// Get activity feed for the current user's teams.
    /// </summary>
    public IReadOnlyList<ActivityFeedItem> GetFeed(int limit = 50, DateTimeOffset? before = null)
    {
        var currentUser = _teamManagement.GetCurrentUser();
        var teams = _teamManagement.GetUserTeams();

        var activities = new List<ActivityFeedItem>();

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();

        var sql = """
            SELECT a.*, u.username, u.display_name, u.email
            FROM activity_log a
            INNER JOIN users u ON a.user_id = u.id
            WHERE (a.team_id IN (SELECT team_id FROM team_memberships WHERE user_id = $user_id)
                   OR a.user_id = $user_id)
            """;

        if (before.HasValue)
        {
            sql += " AND a.occurred_at < $before";
        }

        sql += " ORDER BY a.occurred_at DESC LIMIT $limit";

        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$user_id", currentUser.Id.ToString());
        cmd.Parameters.AddWithValue("$limit", limit);
        if (before.HasValue)
        {
            cmd.Parameters.AddWithValue("$before", before.Value.ToString("O"));
        }

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            activities.Add(MapActivityFeedItem(r));
        }

        return activities;
    }

    /// <summary>
    /// Get activity feed for a specific team.
    /// </summary>
    public IReadOnlyList<ActivityFeedItem> GetTeamFeed(TeamId teamId, int limit = 50, DateTimeOffset? before = null)
    {
        var currentUser = _teamManagement.GetCurrentUser();

        // Verify user is a member
        var membership = _teamManagement.GetMembership(teamId, currentUser.Id);
        if (membership is null)
            throw new UnauthorizedAccessException("Not a member of this team");

        var activities = new List<ActivityFeedItem>();

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();

        var sql = """
            SELECT a.*, u.username, u.display_name, u.email
            FROM activity_log a
            INNER JOIN users u ON a.user_id = u.id
            WHERE a.team_id = $team_id
            """;

        if (before.HasValue)
        {
            sql += " AND a.occurred_at < $before";
        }

        sql += " ORDER BY a.occurred_at DESC LIMIT $limit";

        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$team_id", teamId.ToString());
        cmd.Parameters.AddWithValue("$limit", limit);
        if (before.HasValue)
        {
            cmd.Parameters.AddWithValue("$before", before.Value.ToString("O"));
        }

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            activities.Add(MapActivityFeedItem(r));
        }

        return activities;
    }

    /// <summary>
    /// Get activity feed for a specific resource.
    /// </summary>
    public IReadOnlyList<ActivityFeedItem> GetResourceFeed(SharedResourceId resourceId, int limit = 50)
    {
        var activities = new List<ActivityFeedItem>();

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT a.*, u.username, u.display_name, u.email
            FROM activity_log a
            INNER JOIN users u ON a.user_id = u.id
            WHERE a.resource_id = $resource_id
            ORDER BY a.occurred_at DESC LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$resource_id", resourceId.ToString());
        cmd.Parameters.AddWithValue("$limit", limit);

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            activities.Add(MapActivityFeedItem(r));
        }

        return activities;
    }

    /// <summary>
    /// Get activity for a specific user.
    /// </summary>
    public IReadOnlyList<ActivityFeedItem> GetUserActivity(UserId? userId = null, int limit = 50)
    {
        var currentUser = _teamManagement.GetCurrentUser();
        var targetUserId = userId ?? currentUser.Id;

        var activities = new List<ActivityFeedItem>();

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT a.*, u.username, u.display_name, u.email
            FROM activity_log a
            INNER JOIN users u ON a.user_id = u.id
            WHERE a.user_id = $user_id
            ORDER BY a.occurred_at DESC LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$user_id", targetUserId.ToString());
        cmd.Parameters.AddWithValue("$limit", limit);

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            activities.Add(MapActivityFeedItem(r));
        }

        return activities;
    }

    // ========================================================================
    // Audit Log Operations
    // ========================================================================

    /// <summary>
    /// Get audit log with filtering.
    /// </summary>
    public AuditLogResult GetAuditLog(AuditLogQuery query)
    {
        var currentUser = _teamManagement.GetCurrentUser();

        // Only admins can access full audit log
        if (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.SuperAdmin)
        {
            // Regular users can only see their own activities
            query = query with { UserId = currentUser.Id };
        }

        var entries = new List<AuditLogEntry>();

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();

        var sql = """
            SELECT a.*, u.username, u.display_name, u.email,
                   t.name as team_name,
                   tu.username as target_username
            FROM activity_log a
            INNER JOIN users u ON a.user_id = u.id
            LEFT JOIN teams t ON a.team_id = t.id
            LEFT JOIN users tu ON a.target_user_id = tu.id
            WHERE 1=1
            """;

        if (query.UserId.HasValue)
        {
            sql += " AND a.user_id = $user_id";
            cmd.Parameters.AddWithValue("$user_id", query.UserId.Value.ToString());
        }

        if (query.TeamId.HasValue)
        {
            sql += " AND a.team_id = $team_id";
            cmd.Parameters.AddWithValue("$team_id", query.TeamId.Value.ToString());
        }

        if (query.ResourceId.HasValue)
        {
            sql += " AND a.resource_id = $resource_id";
            cmd.Parameters.AddWithValue("$resource_id", query.ResourceId.Value.ToString());
        }

        if (query.ActivityTypes is { Count: > 0 })
        {
            var types = string.Join(",", query.ActivityTypes.Select((t, i) => $"$type{i}"));
            sql += $" AND a.activity_type IN ({types})";
            for (int i = 0; i < query.ActivityTypes.Count; i++)
            {
                cmd.Parameters.AddWithValue($"$type{i}", query.ActivityTypes[i].ToString());
            }
        }

        if (query.FromDate.HasValue)
        {
            sql += " AND a.occurred_at >= $from_date";
            cmd.Parameters.AddWithValue("$from_date", query.FromDate.Value.ToString("O"));
        }

        if (query.ToDate.HasValue)
        {
            sql += " AND a.occurred_at <= $to_date";
            cmd.Parameters.AddWithValue("$to_date", query.ToDate.Value.ToString("O"));
        }

        if (!string.IsNullOrEmpty(query.SearchTerm))
        {
            sql += " AND a.description LIKE $search";
            cmd.Parameters.AddWithValue("$search", $"%{query.SearchTerm}%");
        }

        // Get total count first
        var countSql = sql.Replace("SELECT a.*, u.username, u.display_name, u.email,\n                   t.name as team_name,\n                   tu.username as target_username", "SELECT COUNT(*)");

        using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = countSql;
            foreach (SqliteParameter p in cmd.Parameters)
            {
                countCmd.Parameters.AddWithValue(p.ParameterName, p.Value);
            }
            var totalCount = Convert.ToInt32(countCmd.ExecuteScalar());
        }

        sql += " ORDER BY a.occurred_at DESC";

        if (query.Limit > 0)
        {
            sql += " LIMIT $limit OFFSET $offset";
            cmd.Parameters.AddWithValue("$limit", query.Limit);
            cmd.Parameters.AddWithValue("$offset", query.Offset);
        }

        cmd.CommandText = sql;

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            entries.Add(MapAuditLogEntry(r));
        }

        return new AuditLogResult(
            Entries: entries,
            TotalCount: entries.Count,
            Query: query
        );
    }

    /// <summary>
    /// Export audit log to JSON.
    /// </summary>
    public string ExportAuditLogJson(AuditLogQuery query)
    {
        var result = GetAuditLog(query);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Get activity summary statistics.
    /// </summary>
    public ActivityStats GetActivityStats(TeamId? teamId = null, DateTimeOffset? fromDate = null, DateTimeOffset? toDate = null)
    {
        using var conn = _db.Open();

        var from = fromDate ?? DateTimeOffset.UtcNow.AddDays(-30);
        var to = toDate ?? DateTimeOffset.UtcNow;

        // Get activity by type
        var activityByType = new Dictionary<ActivityType, int>();
        using (var cmd = conn.CreateCommand())
        {
            var sql = """
                SELECT activity_type, COUNT(*) as count
                FROM activity_log
                WHERE occurred_at >= $from_date AND occurred_at <= $to_date
                """;

            if (teamId.HasValue)
            {
                sql += " AND team_id = $team_id";
            }

            sql += " GROUP BY activity_type";

            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("$from_date", from.ToString("O"));
            cmd.Parameters.AddWithValue("$to_date", to.ToString("O"));
            if (teamId.HasValue)
            {
                cmd.Parameters.AddWithValue("$team_id", teamId.Value.ToString());
            }

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var type = Enum.Parse<ActivityType>(r.GetString(0));
                var count = r.GetInt32(1);
                activityByType[type] = count;
            }
        }

        // Get most active users
        var mostActiveUsers = new List<UserActivityStat>();
        using (var cmd = conn.CreateCommand())
        {
            var sql = """
                SELECT u.id, u.username, u.display_name, COUNT(*) as count
                FROM activity_log a
                INNER JOIN users u ON a.user_id = u.id
                WHERE a.occurred_at >= $from_date AND a.occurred_at <= $to_date
                """;

            if (teamId.HasValue)
            {
                sql += " AND a.team_id = $team_id";
            }

            sql += " GROUP BY u.id, u.username, u.display_name ORDER BY count DESC LIMIT 10";

            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("$from_date", from.ToString("O"));
            cmd.Parameters.AddWithValue("$to_date", to.ToString("O"));
            if (teamId.HasValue)
            {
                cmd.Parameters.AddWithValue("$team_id", teamId.Value.ToString());
            }

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                mostActiveUsers.Add(new UserActivityStat(
                    UserId: new UserId(Guid.Parse(r.GetString(0))),
                    Username: r.GetString(1),
                    DisplayName: r.GetString(2),
                    ActivityCount: r.GetInt32(3)
                ));
            }
        }

        // Get activity by day
        var activityByDay = new Dictionary<DateOnly, int>();
        using (var cmd = conn.CreateCommand())
        {
            var sql = """
                SELECT date(occurred_at) as day, COUNT(*) as count
                FROM activity_log
                WHERE occurred_at >= $from_date AND occurred_at <= $to_date
                """;

            if (teamId.HasValue)
            {
                sql += " AND team_id = $team_id";
            }

            sql += " GROUP BY date(occurred_at) ORDER BY day";

            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("$from_date", from.ToString("O"));
            cmd.Parameters.AddWithValue("$to_date", to.ToString("O"));
            if (teamId.HasValue)
            {
                cmd.Parameters.AddWithValue("$team_id", teamId.Value.ToString());
            }

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var day = DateOnly.Parse(r.GetString(0));
                var count = r.GetInt32(1);
                activityByDay[day] = count;
            }
        }

        // Get total count
        int totalCount;
        using (var cmd = conn.CreateCommand())
        {
            var sql = """
                SELECT COUNT(*) FROM activity_log
                WHERE occurred_at >= $from_date AND occurred_at <= $to_date
                """;

            if (teamId.HasValue)
            {
                sql += " AND team_id = $team_id";
            }

            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("$from_date", from.ToString("O"));
            cmd.Parameters.AddWithValue("$to_date", to.ToString("O"));
            if (teamId.HasValue)
            {
                cmd.Parameters.AddWithValue("$team_id", teamId.Value.ToString());
            }

            totalCount = Convert.ToInt32(cmd.ExecuteScalar());
        }

        return new ActivityStats(
            TotalActivities: totalCount,
            FromDate: from,
            ToDate: to,
            TeamId: teamId,
            ActivityByType: activityByType,
            MostActiveUsers: mostActiveUsers,
            ActivityByDay: activityByDay
        );
    }

    // ========================================================================
    // Notification Operations
    // ========================================================================

    /// <summary>
    /// Get unread notifications for current user.
    /// </summary>
    public IReadOnlyList<Notification> GetUnreadNotifications(int limit = 20)
    {
        var currentUser = _teamManagement.GetCurrentUser();
        return _queries.GetUnreadNotifications(currentUser.Id, limit);
    }

    /// <summary>
    /// Get all notifications for current user.
    /// </summary>
    public IReadOnlyList<Notification> GetNotifications(int limit = 50, bool unreadOnly = false)
    {
        var currentUser = _teamManagement.GetCurrentUser();

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();

        var sql = "SELECT * FROM notifications WHERE user_id = $user_id";
        if (unreadOnly)
        {
            sql += " AND is_read = 0";
        }
        sql += " ORDER BY created_at DESC LIMIT $limit";

        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$user_id", currentUser.Id.ToString());
        cmd.Parameters.AddWithValue("$limit", limit);

        var notifications = new List<Notification>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            notifications.Add(MapNotification(r));
        }

        return notifications;
    }

    /// <summary>
    /// Mark notification as read.
    /// </summary>
    public void MarkNotificationRead(NotificationId notificationId)
    {
        _queries.MarkNotificationRead(notificationId);
    }

    /// <summary>
    /// Mark all notifications as read.
    /// </summary>
    public void MarkAllNotificationsRead()
    {
        var currentUser = _teamManagement.GetCurrentUser();

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE notifications SET is_read = 1, read_at = $read_at
            WHERE user_id = $user_id AND is_read = 0
            """;
        cmd.Parameters.AddWithValue("$user_id", currentUser.Id.ToString());
        cmd.Parameters.AddWithValue("$read_at", DateTimeOffset.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Get notification count.
    /// </summary>
    public NotificationCount GetNotificationCount()
    {
        var currentUser = _teamManagement.GetCurrentUser();

        using var conn = _db.Open();

        int unread, total;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM notifications WHERE user_id = $user_id AND is_read = 0";
            cmd.Parameters.AddWithValue("$user_id", currentUser.Id.ToString());
            unread = Convert.ToInt32(cmd.ExecuteScalar());
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM notifications WHERE user_id = $user_id";
            cmd.Parameters.AddWithValue("$user_id", currentUser.Id.ToString());
            total = Convert.ToInt32(cmd.ExecuteScalar());
        }

        return new NotificationCount(Unread: unread, Total: total);
    }

    /// <summary>
    /// Delete old notifications.
    /// </summary>
    public int DeleteOldNotifications(int daysOld = 30)
    {
        var currentUser = _teamManagement.GetCurrentUser();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-daysOld);

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM notifications
            WHERE user_id = $user_id AND created_at < $cutoff
            """;
        cmd.Parameters.AddWithValue("$user_id", currentUser.Id.ToString());
        cmd.Parameters.AddWithValue("$cutoff", cutoff.ToString("O"));

        return cmd.ExecuteNonQuery();
    }

    // ========================================================================
    // Private Helpers
    // ========================================================================

    private static ActivityFeedItem MapActivityFeedItem(SqliteDataReader r)
    {
        return new ActivityFeedItem(
            Id: new ActivityId(Guid.Parse(r.GetString(r.GetOrdinal("id")))),
            UserId: new UserId(Guid.Parse(r.GetString(r.GetOrdinal("user_id")))),
            Username: r.GetString(r.GetOrdinal("username")),
            DisplayName: r.GetString(r.GetOrdinal("display_name")),
            Type: Enum.Parse<ActivityType>(r.GetString(r.GetOrdinal("activity_type"))),
            Description: r.GetString(r.GetOrdinal("description")),
            TeamId: r.IsDBNull(r.GetOrdinal("team_id")) ? null : new TeamId(Guid.Parse(r.GetString(r.GetOrdinal("team_id")))),
            ResourceId: r.IsDBNull(r.GetOrdinal("resource_id")) ? null : new SharedResourceId(Guid.Parse(r.GetString(r.GetOrdinal("resource_id")))),
            TargetUserId: r.IsDBNull(r.GetOrdinal("target_user_id")) ? null : new UserId(Guid.Parse(r.GetString(r.GetOrdinal("target_user_id")))),
            OccurredAt: DateTimeOffset.Parse(r.GetString(r.GetOrdinal("occurred_at"))),
            Metadata: r.IsDBNull(r.GetOrdinal("metadata")) ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(r.GetString(r.GetOrdinal("metadata")))
        );
    }

    private static AuditLogEntry MapAuditLogEntry(SqliteDataReader r)
    {
        return new AuditLogEntry(
            Id: new ActivityId(Guid.Parse(r.GetString(r.GetOrdinal("id")))),
            UserId: new UserId(Guid.Parse(r.GetString(r.GetOrdinal("user_id")))),
            Username: r.GetString(r.GetOrdinal("username")),
            DisplayName: r.GetString(r.GetOrdinal("display_name")),
            Type: Enum.Parse<ActivityType>(r.GetString(r.GetOrdinal("activity_type"))),
            Description: r.GetString(r.GetOrdinal("description")),
            TeamId: r.IsDBNull(r.GetOrdinal("team_id")) ? null : new TeamId(Guid.Parse(r.GetString(r.GetOrdinal("team_id")))),
            TeamName: r.IsDBNull(r.GetOrdinal("team_name")) ? null : r.GetString(r.GetOrdinal("team_name")),
            ResourceId: r.IsDBNull(r.GetOrdinal("resource_id")) ? null : new SharedResourceId(Guid.Parse(r.GetString(r.GetOrdinal("resource_id")))),
            TargetUserId: r.IsDBNull(r.GetOrdinal("target_user_id")) ? null : new UserId(Guid.Parse(r.GetString(r.GetOrdinal("target_user_id")))),
            TargetUsername: r.IsDBNull(r.GetOrdinal("target_username")) ? null : r.GetString(r.GetOrdinal("target_username")),
            OccurredAt: DateTimeOffset.Parse(r.GetString(r.GetOrdinal("occurred_at"))),
            Metadata: r.IsDBNull(r.GetOrdinal("metadata")) ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(r.GetString(r.GetOrdinal("metadata")))
        );
    }

    private static Notification MapNotification(SqliteDataReader r)
    {
        return new Notification(
            new NotificationId(Guid.Parse(r.GetString(r.GetOrdinal("id")))),
            new UserId(Guid.Parse(r.GetString(r.GetOrdinal("user_id")))),
            Enum.Parse<NotificationType>(r.GetString(r.GetOrdinal("notification_type"))),
            r.GetString(r.GetOrdinal("message")),
            r.GetInt32(r.GetOrdinal("is_read")) == 1,
            r.IsDBNull(r.GetOrdinal("team_id")) ? null : new TeamId(Guid.Parse(r.GetString(r.GetOrdinal("team_id")))),
            r.IsDBNull(r.GetOrdinal("resource_id")) ? null : new SharedResourceId(Guid.Parse(r.GetString(r.GetOrdinal("resource_id")))),
            r.IsDBNull(r.GetOrdinal("comment_id")) ? null : new CommentId(Guid.Parse(r.GetString(r.GetOrdinal("comment_id")))),
            DateTimeOffset.Parse(r.GetString(r.GetOrdinal("created_at"))),
            r.IsDBNull(r.GetOrdinal("read_at")) ? null : DateTimeOffset.Parse(r.GetString(r.GetOrdinal("read_at"))),
            r.IsDBNull(r.GetOrdinal("metadata")) ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(r.GetString(r.GetOrdinal("metadata")))
        );
    }
}

// DTOs
public sealed record ActivityFeedItem(
    ActivityId Id,
    UserId UserId,
    string Username,
    string DisplayName,
    ActivityType Type,
    string Description,
    TeamId? TeamId,
    SharedResourceId? ResourceId,
    UserId? TargetUserId,
    DateTimeOffset OccurredAt,
    Dictionary<string, object>? Metadata
);

public sealed record AuditLogEntry(
    ActivityId Id,
    UserId UserId,
    string Username,
    string DisplayName,
    ActivityType Type,
    string Description,
    TeamId? TeamId,
    string? TeamName,
    SharedResourceId? ResourceId,
    UserId? TargetUserId,
    string? TargetUsername,
    DateTimeOffset OccurredAt,
    Dictionary<string, object>? Metadata
);

public sealed record AuditLogQuery(
    UserId? UserId = null,
    TeamId? TeamId = null,
    SharedResourceId? ResourceId = null,
    List<ActivityType>? ActivityTypes = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    string? SearchTerm = null,
    int Limit = 100,
    int Offset = 0
);

public sealed record AuditLogResult(
    IReadOnlyList<AuditLogEntry> Entries,
    int TotalCount,
    AuditLogQuery Query
);

public sealed record ActivityStats(
    int TotalActivities,
    DateTimeOffset FromDate,
    DateTimeOffset ToDate,
    TeamId? TeamId,
    Dictionary<ActivityType, int> ActivityByType,
    List<UserActivityStat> MostActiveUsers,
    Dictionary<DateOnly, int> ActivityByDay
);

public sealed record UserActivityStat(
    UserId UserId,
    string Username,
    string DisplayName,
    int ActivityCount
);

public sealed record NotificationCount(
    int Unread,
    int Total
);
