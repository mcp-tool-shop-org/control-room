using System.Text.Json;
using Microsoft.Data.Sqlite;
using ControlRoom.Domain.Model;

namespace ControlRoom.Infrastructure.Storage.Queries;

/// <summary>
/// Database queries for team collaboration features.
/// </summary>
public sealed class TeamQueries
{
    private readonly Db _db;
    public TeamQueries(Db db) => _db = db;

    // ========================================================================
    // User Operations
    // ========================================================================

    public User? GetUser(UserId userId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM users WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", userId.ToString());

        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;

        return MapUser(r);
    }

    public User? GetUserByUsername(string username)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM users WHERE username = $username";
        cmd.Parameters.AddWithValue("$username", username);

        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;

        return MapUser(r);
    }

    public User? GetUserByEmail(string email)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM users WHERE email = $email";
        cmd.Parameters.AddWithValue("$email", email);

        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;

        return MapUser(r);
    }

    public IReadOnlyList<User> SearchUsers(string query, int limit = 20)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM users
            WHERE username LIKE $query OR display_name LIKE $query OR email LIKE $query
            ORDER BY display_name
            LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$query", $"%{query}%");
        cmd.Parameters.AddWithValue("$limit", limit);

        var users = new List<User>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            users.Add(MapUser(r));
        }
        return users;
    }

    public void InsertUser(User user)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO users (id, username, display_name, email, role, created_at, last_login_at, preferences)
            VALUES ($id, $username, $display_name, $email, $role, $created_at, $last_login_at, $preferences)
            """;
        cmd.Parameters.AddWithValue("$id", user.Id.ToString());
        cmd.Parameters.AddWithValue("$username", user.Username);
        cmd.Parameters.AddWithValue("$display_name", user.DisplayName);
        cmd.Parameters.AddWithValue("$email", user.Email);
        cmd.Parameters.AddWithValue("$role", user.Role.ToString());
        cmd.Parameters.AddWithValue("$created_at", user.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$last_login_at", user.LastLoginAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$preferences", JsonSerializer.Serialize(user.Preferences));
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    public void UpdateUser(User user)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE users SET display_name = $display_name, email = $email,
            last_login_at = $last_login_at, preferences = $preferences
            WHERE id = $id
            """;
        cmd.Parameters.AddWithValue("$id", user.Id.ToString());
        cmd.Parameters.AddWithValue("$display_name", user.DisplayName);
        cmd.Parameters.AddWithValue("$email", user.Email);
        cmd.Parameters.AddWithValue("$last_login_at", user.LastLoginAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$preferences", JsonSerializer.Serialize(user.Preferences));
        cmd.ExecuteNonQuery();
    }

    // ========================================================================
    // Team Operations
    // ========================================================================

    public Team? GetTeam(TeamId teamId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM teams WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", teamId.ToString());

        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;

        var team = MapTeam(r);
        var members = GetTeamMemberships(teamId);
        return team with { Members = members.ToList() };
    }

    public IReadOnlyList<Team> GetUserTeams(UserId userId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT t.* FROM teams t
            INNER JOIN team_memberships tm ON t.id = tm.team_id
            WHERE tm.user_id = $user_id
            ORDER BY t.name
            """;
        cmd.Parameters.AddWithValue("$user_id", userId.ToString());

        var teams = new List<Team>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var team = MapTeam(r);
            teams.Add(team);
        }

        // Load members for each team
        return teams.Select(t =>
        {
            var members = GetTeamMemberships(t.Id);
            return t with { Members = members.ToList() };
        }).ToList();
    }

    public void InsertTeam(Team team)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO teams (id, name, description, owner_id, created_at, updated_at, settings)
            VALUES ($id, $name, $description, $owner_id, $created_at, $updated_at, $settings)
            """;
        cmd.Parameters.AddWithValue("$id", team.Id.ToString());
        cmd.Parameters.AddWithValue("$name", team.Name);
        cmd.Parameters.AddWithValue("$description", team.Description ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$owner_id", team.OwnerId.ToString());
        cmd.Parameters.AddWithValue("$created_at", team.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$updated_at", team.UpdatedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$settings", JsonSerializer.Serialize(team.Settings));
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    public void UpdateTeam(Team team)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE teams SET name = $name, description = $description,
            updated_at = $updated_at, settings = $settings
            WHERE id = $id
            """;
        cmd.Parameters.AddWithValue("$id", team.Id.ToString());
        cmd.Parameters.AddWithValue("$name", team.Name);
        cmd.Parameters.AddWithValue("$description", team.Description ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$updated_at", team.UpdatedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$settings", JsonSerializer.Serialize(team.Settings));
        cmd.ExecuteNonQuery();
    }

    public void DeleteTeam(TeamId teamId)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();

        // Delete memberships first
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM team_memberships WHERE team_id = $id";
            cmd.Parameters.AddWithValue("$id", teamId.ToString());
            cmd.ExecuteNonQuery();
        }

        // Delete invitations
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM team_invitations WHERE team_id = $id";
            cmd.Parameters.AddWithValue("$id", teamId.ToString());
            cmd.ExecuteNonQuery();
        }

        // Delete team
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM teams WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", teamId.ToString());
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    // ========================================================================
    // Membership Operations
    // ========================================================================

    public IReadOnlyList<TeamMembership> GetTeamMemberships(TeamId teamId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM team_memberships WHERE team_id = $team_id";
        cmd.Parameters.AddWithValue("$team_id", teamId.ToString());

        var memberships = new List<TeamMembership>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            memberships.Add(MapMembership(r));
        }
        return memberships;
    }

    public TeamMembership? GetMembership(TeamId teamId, UserId userId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM team_memberships WHERE team_id = $team_id AND user_id = $user_id";
        cmd.Parameters.AddWithValue("$team_id", teamId.ToString());
        cmd.Parameters.AddWithValue("$user_id", userId.ToString());

        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;

        return MapMembership(r);
    }

    public void InsertMembership(TeamMembership membership)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO team_memberships (id, team_id, user_id, role, added_by, joined_at)
            VALUES ($id, $team_id, $user_id, $role, $added_by, $joined_at)
            """;
        cmd.Parameters.AddWithValue("$id", membership.Id.ToString());
        cmd.Parameters.AddWithValue("$team_id", membership.TeamId.ToString());
        cmd.Parameters.AddWithValue("$user_id", membership.UserId.ToString());
        cmd.Parameters.AddWithValue("$role", membership.Role.ToString());
        cmd.Parameters.AddWithValue("$added_by", membership.AddedBy.ToString());
        cmd.Parameters.AddWithValue("$joined_at", membership.JoinedAt.ToString("O"));
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    public void UpdateMembershipRole(TeamId teamId, UserId userId, TeamRole newRole)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE team_memberships SET role = $role WHERE team_id = $team_id AND user_id = $user_id";
        cmd.Parameters.AddWithValue("$team_id", teamId.ToString());
        cmd.Parameters.AddWithValue("$user_id", userId.ToString());
        cmd.Parameters.AddWithValue("$role", newRole.ToString());
        cmd.ExecuteNonQuery();
    }

    public void DeleteMembership(TeamId teamId, UserId userId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM team_memberships WHERE team_id = $team_id AND user_id = $user_id";
        cmd.Parameters.AddWithValue("$team_id", teamId.ToString());
        cmd.Parameters.AddWithValue("$user_id", userId.ToString());
        cmd.ExecuteNonQuery();
    }

    // ========================================================================
    // Invitation Operations
    // ========================================================================

    public TeamInvitation? GetInvitation(TeamInvitationId invitationId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM team_invitations WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", invitationId.ToString());

        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;

        return MapInvitation(r);
    }

    public IReadOnlyList<TeamInvitation> GetPendingInvitations(TeamId teamId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM team_invitations WHERE team_id = $team_id AND status = 'Pending'";
        cmd.Parameters.AddWithValue("$team_id", teamId.ToString());

        var invitations = new List<TeamInvitation>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            invitations.Add(MapInvitation(r));
        }
        return invitations;
    }

    public IReadOnlyList<TeamInvitation> GetUserInvitations(UserId userId, string email)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM team_invitations
            WHERE (email = $email OR invited_user_id = $user_id) AND status = 'Pending'
            ORDER BY created_at DESC
            """;
        cmd.Parameters.AddWithValue("$email", email);
        cmd.Parameters.AddWithValue("$user_id", userId.ToString());

        var invitations = new List<TeamInvitation>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            invitations.Add(MapInvitation(r));
        }
        return invitations;
    }

    public void InsertInvitation(TeamInvitation invitation)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO team_invitations (id, team_id, email, invited_user_id, role, invited_by, created_at, expires_at, status)
            VALUES ($id, $team_id, $email, $invited_user_id, $role, $invited_by, $created_at, $expires_at, $status)
            """;
        cmd.Parameters.AddWithValue("$id", invitation.Id.ToString());
        cmd.Parameters.AddWithValue("$team_id", invitation.TeamId.ToString());
        cmd.Parameters.AddWithValue("$email", invitation.Email);
        cmd.Parameters.AddWithValue("$invited_user_id", invitation.InvitedUserId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$role", invitation.Role.ToString());
        cmd.Parameters.AddWithValue("$invited_by", invitation.InvitedBy.ToString());
        cmd.Parameters.AddWithValue("$created_at", invitation.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$expires_at", invitation.ExpiresAt.ToString("O"));
        cmd.Parameters.AddWithValue("$status", invitation.Status.ToString());
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    public void UpdateInvitationStatus(TeamInvitationId invitationId, InvitationStatus status)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE team_invitations SET status = $status WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", invitationId.ToString());
        cmd.Parameters.AddWithValue("$status", status.ToString());
        cmd.ExecuteNonQuery();
    }

    // ========================================================================
    // Activity Log Operations
    // ========================================================================

    public void InsertActivity(ActivityEntry entry)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO activity_log (id, user_id, activity_type, description, team_id, resource_id, target_user_id, comment_id, occurred_at, metadata)
            VALUES ($id, $user_id, $activity_type, $description, $team_id, $resource_id, $target_user_id, $comment_id, $occurred_at, $metadata)
            """;
        cmd.Parameters.AddWithValue("$id", entry.Id.ToString());
        cmd.Parameters.AddWithValue("$user_id", entry.UserId.ToString());
        cmd.Parameters.AddWithValue("$activity_type", entry.Type.ToString());
        cmd.Parameters.AddWithValue("$description", entry.Description);
        cmd.Parameters.AddWithValue("$team_id", entry.TeamId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$resource_id", entry.ResourceId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$target_user_id", entry.TargetUserId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$comment_id", entry.CommentId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$occurred_at", entry.OccurredAt.ToString("O"));
        cmd.Parameters.AddWithValue("$metadata", entry.Metadata != null ? JsonSerializer.Serialize(entry.Metadata) : DBNull.Value);
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    public IReadOnlyList<ActivityEntry> GetActivities(TeamId? teamId = null, UserId? userId = null, int limit = 50)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();

        var sql = "SELECT * FROM activity_log WHERE 1=1";
        if (teamId.HasValue)
        {
            sql += " AND team_id = $team_id";
            cmd.Parameters.AddWithValue("$team_id", teamId.Value.ToString());
        }
        if (userId.HasValue)
        {
            sql += " AND user_id = $user_id";
            cmd.Parameters.AddWithValue("$user_id", userId.Value.ToString());
        }
        sql += " ORDER BY occurred_at DESC LIMIT $limit";
        cmd.Parameters.AddWithValue("$limit", limit);

        cmd.CommandText = sql;

        var activities = new List<ActivityEntry>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            activities.Add(MapActivity(r));
        }
        return activities;
    }

    // ========================================================================
    // Notification Operations
    // ========================================================================

    public void InsertNotification(Notification notification)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO notifications (id, user_id, notification_type, message, is_read, team_id, resource_id, comment_id, created_at, read_at, metadata)
            VALUES ($id, $user_id, $notification_type, $message, $is_read, $team_id, $resource_id, $comment_id, $created_at, $read_at, $metadata)
            """;
        cmd.Parameters.AddWithValue("$id", notification.Id.ToString());
        cmd.Parameters.AddWithValue("$user_id", notification.UserId.ToString());
        cmd.Parameters.AddWithValue("$notification_type", notification.Type.ToString());
        cmd.Parameters.AddWithValue("$message", notification.Message);
        cmd.Parameters.AddWithValue("$is_read", notification.IsRead ? 1 : 0);
        cmd.Parameters.AddWithValue("$team_id", notification.TeamId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$resource_id", notification.ResourceId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$comment_id", notification.CommentId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$created_at", notification.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$read_at", notification.ReadAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$metadata", notification.Metadata != null ? JsonSerializer.Serialize(notification.Metadata) : DBNull.Value);
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    public IReadOnlyList<Notification> GetUnreadNotifications(UserId userId, int limit = 20)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM notifications WHERE user_id = $user_id AND is_read = 0
            ORDER BY created_at DESC LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$user_id", userId.ToString());
        cmd.Parameters.AddWithValue("$limit", limit);

        var notifications = new List<Notification>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            notifications.Add(MapNotification(r));
        }
        return notifications;
    }

    public void MarkNotificationRead(NotificationId notificationId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE notifications SET is_read = 1, read_at = $read_at WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", notificationId.ToString());
        cmd.Parameters.AddWithValue("$read_at", DateTimeOffset.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    // ========================================================================
    // Mapping Helpers
    // ========================================================================

    private static User MapUser(SqliteDataReader r) => new(
        new UserId(Guid.Parse(r.GetString(r.GetOrdinal("id")))),
        r.GetString(r.GetOrdinal("username")),
        r.GetString(r.GetOrdinal("display_name")),
        r.GetString(r.GetOrdinal("email")),
        Enum.Parse<UserRole>(r.GetString(r.GetOrdinal("role"))),
        DateTimeOffset.Parse(r.GetString(r.GetOrdinal("created_at"))),
        r.IsDBNull(r.GetOrdinal("last_login_at")) ? null : DateTimeOffset.Parse(r.GetString(r.GetOrdinal("last_login_at"))),
        JsonSerializer.Deserialize<Dictionary<string, string>>(r.GetString(r.GetOrdinal("preferences"))) ?? new()
    );

    private static Team MapTeam(SqliteDataReader r) => new(
        new TeamId(Guid.Parse(r.GetString(r.GetOrdinal("id")))),
        r.GetString(r.GetOrdinal("name")),
        r.IsDBNull(r.GetOrdinal("description")) ? "" : r.GetString(r.GetOrdinal("description")),
        new UserId(Guid.Parse(r.GetString(r.GetOrdinal("owner_id")))),
        DateTimeOffset.Parse(r.GetString(r.GetOrdinal("created_at"))),
        r.IsDBNull(r.GetOrdinal("updated_at")) ? null : DateTimeOffset.Parse(r.GetString(r.GetOrdinal("updated_at"))),
        JsonSerializer.Deserialize<Dictionary<string, string>>(r.GetString(r.GetOrdinal("settings"))) ?? new(),
        new List<TeamMembership>()
    );

    private static TeamMembership MapMembership(SqliteDataReader r) => new(
        new TeamMembershipId(Guid.Parse(r.GetString(r.GetOrdinal("id")))),
        new TeamId(Guid.Parse(r.GetString(r.GetOrdinal("team_id")))),
        new UserId(Guid.Parse(r.GetString(r.GetOrdinal("user_id")))),
        Enum.Parse<TeamRole>(r.GetString(r.GetOrdinal("role"))),
        new UserId(Guid.Parse(r.GetString(r.GetOrdinal("added_by")))),
        DateTimeOffset.Parse(r.GetString(r.GetOrdinal("joined_at")))
    );

    private static TeamInvitation MapInvitation(SqliteDataReader r) => new(
        new TeamInvitationId(Guid.Parse(r.GetString(r.GetOrdinal("id")))),
        new TeamId(Guid.Parse(r.GetString(r.GetOrdinal("team_id")))),
        r.GetString(r.GetOrdinal("email")),
        r.IsDBNull(r.GetOrdinal("invited_user_id")) ? null : new UserId(Guid.Parse(r.GetString(r.GetOrdinal("invited_user_id")))),
        Enum.Parse<TeamRole>(r.GetString(r.GetOrdinal("role"))),
        new UserId(Guid.Parse(r.GetString(r.GetOrdinal("invited_by")))),
        DateTimeOffset.Parse(r.GetString(r.GetOrdinal("created_at"))),
        DateTimeOffset.Parse(r.GetString(r.GetOrdinal("expires_at"))),
        Enum.Parse<InvitationStatus>(r.GetString(r.GetOrdinal("status")))
    );

    private static ActivityEntry MapActivity(SqliteDataReader r) => new(
        new ActivityId(Guid.Parse(r.GetString(r.GetOrdinal("id")))),
        new UserId(Guid.Parse(r.GetString(r.GetOrdinal("user_id")))),
        Enum.Parse<ActivityType>(r.GetString(r.GetOrdinal("activity_type"))),
        r.GetString(r.GetOrdinal("description")),
        r.IsDBNull(r.GetOrdinal("team_id")) ? null : new TeamId(Guid.Parse(r.GetString(r.GetOrdinal("team_id")))),
        r.IsDBNull(r.GetOrdinal("resource_id")) ? null : new SharedResourceId(Guid.Parse(r.GetString(r.GetOrdinal("resource_id")))),
        r.IsDBNull(r.GetOrdinal("target_user_id")) ? null : new UserId(Guid.Parse(r.GetString(r.GetOrdinal("target_user_id")))),
        r.IsDBNull(r.GetOrdinal("comment_id")) ? null : new CommentId(Guid.Parse(r.GetString(r.GetOrdinal("comment_id")))),
        DateTimeOffset.Parse(r.GetString(r.GetOrdinal("occurred_at"))),
        r.IsDBNull(r.GetOrdinal("metadata")) ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(r.GetString(r.GetOrdinal("metadata")))
    );

    private static Notification MapNotification(SqliteDataReader r) => new(
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
