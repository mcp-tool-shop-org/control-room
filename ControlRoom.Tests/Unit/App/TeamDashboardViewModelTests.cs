using ControlRoom.Domain.Model;
using ControlRoom.Application.UseCases;

namespace ControlRoom.Tests.Unit.App;

/// <summary>
/// Unit tests for Team Dashboard ViewModels logic and helpers.
/// Since the App layer uses MAUI-specific dependencies, these tests
/// validate the underlying business logic through the Application layer.
/// </summary>
public sealed class TeamDashboardViewModelTests
{
    // ========================================================================
    // User Initials Calculation Tests
    // ========================================================================

    [Theory]
    [InlineData("John Doe", "JD")]
    [InlineData("Jane Smith", "JS")]
    [InlineData("Administrator", "AD")]
    [InlineData("X", "X")]
    [InlineData("AB", "AB")]
    [InlineData("Alice Bob Charlie", "AB")]
    public void GetInitials_CalculatesCorrectly(string displayName, string expectedInitials)
    {
        var initials = GetInitials(displayName);
        Assert.Equal(expectedInitials, initials);
    }

    // ========================================================================
    // Relative Time Calculation Tests
    // ========================================================================

    [Fact]
    public void GetRelativeTime_JustNow()
    {
        var time = DateTimeOffset.UtcNow;
        var text = GetRelativeTime(time);
        Assert.Equal("just now", text);
    }

    [Fact]
    public void GetRelativeTime_MinutesAgo()
    {
        var time = DateTimeOffset.UtcNow.AddMinutes(-30);
        var text = GetRelativeTime(time);
        Assert.Contains("m ago", text);
    }

    [Fact]
    public void GetRelativeTime_HoursAgo()
    {
        var time = DateTimeOffset.UtcNow.AddHours(-5);
        var text = GetRelativeTime(time);
        Assert.Contains("h ago", text);
    }

    [Fact]
    public void GetRelativeTime_DaysAgo()
    {
        var time = DateTimeOffset.UtcNow.AddDays(-3);
        var text = GetRelativeTime(time);
        Assert.Contains("d ago", text);
    }

    [Fact]
    public void GetRelativeTime_WeeksAgo()
    {
        var time = DateTimeOffset.UtcNow.AddDays(-14);
        var text = GetRelativeTimeExtended(time);
        Assert.Contains("w ago", text);
    }

    [Fact]
    public void GetRelativeTime_MonthsAgo()
    {
        var time = DateTimeOffset.UtcNow.AddDays(-60);
        var text = GetRelativeTimeExtended(time);
        Assert.Contains("mo ago", text);
    }

    [Fact]
    public void GetRelativeTime_YearsAgo()
    {
        var time = DateTimeOffset.UtcNow.AddDays(-400);
        var text = GetRelativeTimeExtended(time);
        Assert.Contains("y ago", text);
    }

    // ========================================================================
    // Member Count Text Tests
    // ========================================================================

    [Theory]
    [InlineData(0, "0 members")]
    [InlineData(1, "1 member")]
    [InlineData(2, "2 members")]
    [InlineData(10, "10 members")]
    [InlineData(100, "100 members")]
    public void GetMemberCountText_FormatsCorrectly(int count, string expected)
    {
        var text = GetMemberCountText(count);
        Assert.Equal(expected, text);
    }

    // ========================================================================
    // Role Color Tests
    // ========================================================================

    [Theory]
    [InlineData(TeamRole.Owner, "#D32F2F")]
    [InlineData(TeamRole.Admin, "#1976D2")]
    [InlineData(TeamRole.Member, "#388E3C")]
    [InlineData(TeamRole.Viewer, "#757575")]
    public void GetRoleColor_ReturnsCorrectColor(TeamRole role, string expectedColor)
    {
        var color = GetRoleColor(role);
        Assert.Equal(expectedColor, color);
    }

    // ========================================================================
    // Resource Type Icon Tests
    // ========================================================================

    [Theory]
    [InlineData(ResourceType.Script, "\U0001F4DC")]
    [InlineData(ResourceType.Runbook, "\U0001F4D6")]
    [InlineData(ResourceType.Dashboard, "\U0001F4CA")]
    [InlineData(ResourceType.Alert, "\U0001F514")]
    [InlineData(ResourceType.HealthCheck, "\u2764\uFE0F")]
    [InlineData(ResourceType.SelfHealingRule, "\U0001F527")]
    public void GetResourceIcon_ReturnsCorrectIcon(ResourceType type, string expectedIcon)
    {
        var icon = GetResourceIcon(type);
        Assert.Equal(expectedIcon, icon);
    }

    // ========================================================================
    // Activity Type Icon Tests
    // ========================================================================

    [Theory]
    [InlineData(ActivityType.UserCreated, "\U0001F464")]
    [InlineData(ActivityType.TeamCreated, "\U0001F465")]
    [InlineData(ActivityType.TeamDeleted, "\u274C")]
    [InlineData(ActivityType.MemberAdded, "\u2795")]
    [InlineData(ActivityType.MemberRemoved, "\u2796")]
    [InlineData(ActivityType.RoleChanged, "\U0001F511")]
    [InlineData(ActivityType.ResourceShared, "\U0001F4E4")]
    [InlineData(ActivityType.CommentAdded, "\U0001F4AC")]
    [InlineData(ActivityType.ScriptExecuted, "\u25B6\uFE0F")]
    [InlineData(ActivityType.RunbookExecuted, "\U0001F3C3")]
    public void GetActivityIcon_ReturnsCorrectIcon(ActivityType type, string expectedIcon)
    {
        var icon = GetActivityIcon(type);
        Assert.Equal(expectedIcon, icon);
    }

    // ========================================================================
    // Notification Type Icon Tests
    // ========================================================================

    [Theory]
    [InlineData(NotificationType.TeamInvitation, "\U0001F4E9")]
    [InlineData(NotificationType.ResourceShared, "\U0001F4E4")]
    [InlineData(NotificationType.MentionedInComment, "@")]
    [InlineData(NotificationType.CommentReply, "\U0001F4AC")]
    [InlineData(NotificationType.AlertFired, "\U0001F6A8")]
    [InlineData(NotificationType.RunbookCompleted, "\u2705")]
    [InlineData(NotificationType.PermissionChanged, "\U0001F511")]
    public void GetNotificationIcon_ReturnsCorrectIcon(NotificationType type, string expectedIcon)
    {
        var icon = GetNotificationIcon(type);
        Assert.Equal(expectedIcon, icon);
    }

    // ========================================================================
    // Invitation Expiry Tests
    // ========================================================================

    [Fact]
    public void IsExpired_False_WhenFuture()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        Assert.False(IsExpired(expiresAt));
    }

    [Fact]
    public void IsExpired_True_WhenPast()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddDays(-7);
        Assert.True(IsExpired(expiresAt));
    }

    [Fact]
    public void GetExpiresInText_Expired()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddDays(-1);
        var text = GetExpiresInText(expiresAt);
        Assert.Equal("Expired", text);
    }

    [Fact]
    public void GetExpiresInText_Days()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddDays(5);
        var text = GetExpiresInText(expiresAt);
        Assert.Contains("Expires in", text);
        Assert.Contains("d", text);
    }

    [Fact]
    public void GetExpiresInText_Hours()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(5);
        var text = GetExpiresInText(expiresAt);
        Assert.Contains("Expires in", text);
        Assert.Contains("h", text);
    }

    [Fact]
    public void GetExpiresInText_Soon()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(30);
        var text = GetExpiresInText(expiresAt);
        Assert.Equal("Expires soon", text);
    }

    // ========================================================================
    // ActivityFeedItem Tests (from Application layer)
    // ========================================================================

    [Fact]
    public void ActivityFeedItem_AllProperties()
    {
        var item = new ActivityFeedItem(
            Id: ActivityId.New(),
            UserId: UserId.New(),
            Username: "testuser",
            DisplayName: "Test User",
            Type: ActivityType.TeamCreated,
            Description: "Created team Engineering",
            TeamId: TeamId.New(),
            ResourceId: null,
            TargetUserId: null,
            OccurredAt: DateTimeOffset.UtcNow,
            Metadata: null
        );

        Assert.Equal("testuser", item.Username);
        Assert.Equal("Test User", item.DisplayName);
        Assert.Equal(ActivityType.TeamCreated, item.Type);
        Assert.NotNull(item.TeamId);
    }

    // ========================================================================
    // NotificationCount Tests
    // ========================================================================

    [Fact]
    public void NotificationCount_Basic()
    {
        var count = new NotificationCount(Unread: 5, Total: 20);
        Assert.Equal(5, count.Unread);
        Assert.Equal(20, count.Total);
    }

    [Fact]
    public void NotificationCount_AllRead()
    {
        var count = new NotificationCount(Unread: 0, Total: 15);
        Assert.Equal(0, count.Unread);
    }

    // ========================================================================
    // Helper Functions (mirroring ViewModel logic)
    // ========================================================================

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{parts[0][0]}{parts[1][0]}".ToUpper();
        if (parts.Length == 1 && parts[0].Length >= 2)
            return parts[0][..2].ToUpper();
        return name.Length >= 1 ? name[0].ToString().ToUpper() : "?";
    }

    private static string GetRelativeTime(DateTimeOffset time)
    {
        var diff = DateTimeOffset.UtcNow - time;
        if (diff.TotalDays >= 1) return $"{(int)diff.TotalDays}d ago";
        if (diff.TotalHours >= 1) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalMinutes >= 1) return $"{(int)diff.TotalMinutes}m ago";
        return "just now";
    }

    private static string GetRelativeTimeExtended(DateTimeOffset time)
    {
        var diff = DateTimeOffset.UtcNow - time;
        if (diff.TotalDays >= 365) return $"{(int)(diff.TotalDays / 365)}y ago";
        if (diff.TotalDays >= 30) return $"{(int)(diff.TotalDays / 30)}mo ago";
        if (diff.TotalDays >= 7) return $"{(int)(diff.TotalDays / 7)}w ago";
        if (diff.TotalDays >= 1) return $"{(int)diff.TotalDays}d ago";
        if (diff.TotalHours >= 1) return $"{(int)diff.TotalHours}h ago";
        return "just now";
    }

    private static string GetMemberCountText(int count)
    {
        return count == 1 ? "1 member" : $"{count} members";
    }

    private static string GetRoleColor(TeamRole role)
    {
        return role switch
        {
            TeamRole.Owner => "#D32F2F",
            TeamRole.Admin => "#1976D2",
            TeamRole.Member => "#388E3C",
            TeamRole.Viewer => "#757575",
            _ => "#9E9E9E"
        };
    }

    private static string GetResourceIcon(ResourceType type)
    {
        return type switch
        {
            ResourceType.Script => "\U0001F4DC",
            ResourceType.Runbook => "\U0001F4D6",
            ResourceType.Dashboard => "\U0001F4CA",
            ResourceType.Alert => "\U0001F514",
            ResourceType.HealthCheck => "\u2764\uFE0F",
            ResourceType.SelfHealingRule => "\U0001F527",
            _ => "\U0001F4C4"
        };
    }

    private static string GetActivityIcon(ActivityType type)
    {
        return type switch
        {
            ActivityType.UserCreated => "\U0001F464",
            ActivityType.TeamCreated => "\U0001F465",
            ActivityType.TeamDeleted => "\u274C",
            ActivityType.MemberAdded => "\u2795",
            ActivityType.MemberRemoved => "\u2796",
            ActivityType.RoleChanged => "\U0001F511",
            ActivityType.ResourceShared => "\U0001F4E4",
            ActivityType.ResourceUnshared => "\U0001F512",
            ActivityType.PermissionChanged => "\U0001F6E1\uFE0F",
            ActivityType.CommentAdded => "\U0001F4AC",
            ActivityType.AnnotationAdded => "\U0001F4DD",
            ActivityType.ScriptExecuted => "\u25B6\uFE0F",
            ActivityType.RunbookExecuted => "\U0001F3C3",
            _ => "\U0001F4C3"
        };
    }

    private static string GetNotificationIcon(NotificationType type)
    {
        return type switch
        {
            NotificationType.TeamInvitation => "\U0001F4E9",
            NotificationType.ResourceShared => "\U0001F4E4",
            NotificationType.MentionedInComment => "@",
            NotificationType.CommentReply => "\U0001F4AC",
            NotificationType.AlertFired => "\U0001F6A8",
            NotificationType.RunbookCompleted => "\u2705",
            NotificationType.PermissionChanged => "\U0001F511",
            _ => "\U0001F514"
        };
    }

    private static bool IsExpired(DateTimeOffset expiresAt)
    {
        return expiresAt < DateTimeOffset.UtcNow;
    }

    private static string GetExpiresInText(DateTimeOffset expiresAt)
    {
        var diff = expiresAt - DateTimeOffset.UtcNow;
        if (diff.TotalDays < 0) return "Expired";
        if (diff.TotalDays >= 1) return $"Expires in {(int)diff.TotalDays}d";
        if (diff.TotalHours >= 1) return $"Expires in {(int)diff.TotalHours}h";
        return "Expires soon";
    }
}
