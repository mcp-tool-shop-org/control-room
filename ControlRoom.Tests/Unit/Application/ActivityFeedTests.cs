using ControlRoom.Domain.Model;
using ControlRoom.Application.UseCases;

namespace ControlRoom.Tests.Unit.Application;

/// <summary>
/// Unit tests for ActivityFeed and Audit Log use case.
/// </summary>
public sealed class ActivityFeedTests
{
    // ========================================================================
    // ActivityFeedItem Tests
    // ========================================================================

    [Fact]
    public void ActivityFeedItem_BasicConstruction()
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
        Assert.Equal("Created team Engineering", item.Description);
        Assert.NotNull(item.TeamId);
        Assert.Null(item.ResourceId);
    }

    [Fact]
    public void ActivityFeedItem_WithMetadata()
    {
        var metadata = new Dictionary<string, object>
        {
            ["previousRole"] = "Member",
            ["newRole"] = "Admin"
        };

        var item = new ActivityFeedItem(
            Id: ActivityId.New(),
            UserId: UserId.New(),
            Username: "admin",
            DisplayName: "Admin User",
            Type: ActivityType.RoleChanged,
            Description: "Role changed",
            TeamId: TeamId.New(),
            ResourceId: null,
            TargetUserId: UserId.New(),
            OccurredAt: DateTimeOffset.UtcNow,
            Metadata: metadata
        );

        Assert.NotNull(item.Metadata);
        Assert.Equal("Member", item.Metadata["previousRole"]);
        Assert.Equal("Admin", item.Metadata["newRole"]);
        Assert.NotNull(item.TargetUserId);
    }

    // ========================================================================
    // AuditLogEntry Tests
    // ========================================================================

    [Fact]
    public void AuditLogEntry_BasicConstruction()
    {
        var entry = new AuditLogEntry(
            Id: ActivityId.New(),
            UserId: UserId.New(),
            Username: "auditor",
            DisplayName: "Auditor User",
            Type: ActivityType.ResourceShared,
            Description: "Script shared with team",
            TeamId: TeamId.New(),
            TeamName: "Engineering",
            ResourceId: SharedResourceId.New(),
            TargetUserId: null,
            TargetUsername: null,
            OccurredAt: DateTimeOffset.UtcNow,
            Metadata: null
        );

        Assert.Equal("auditor", entry.Username);
        Assert.Equal(ActivityType.ResourceShared, entry.Type);
        Assert.Equal("Engineering", entry.TeamName);
        Assert.NotNull(entry.ResourceId);
    }

    [Fact]
    public void AuditLogEntry_WithTargetUser()
    {
        var entry = new AuditLogEntry(
            Id: ActivityId.New(),
            UserId: UserId.New(),
            Username: "admin",
            DisplayName: "Admin",
            Type: ActivityType.MemberAdded,
            Description: "Member added to team",
            TeamId: TeamId.New(),
            TeamName: "DevOps",
            ResourceId: null,
            TargetUserId: UserId.New(),
            TargetUsername: "newmember",
            OccurredAt: DateTimeOffset.UtcNow,
            Metadata: new Dictionary<string, object> { ["role"] = "Member" }
        );

        Assert.NotNull(entry.TargetUserId);
        Assert.Equal("newmember", entry.TargetUsername);
        Assert.NotNull(entry.Metadata);
    }

    // ========================================================================
    // AuditLogQuery Tests
    // ========================================================================

    [Fact]
    public void AuditLogQuery_DefaultValues()
    {
        var query = new AuditLogQuery();

        Assert.Null(query.UserId);
        Assert.Null(query.TeamId);
        Assert.Null(query.ResourceId);
        Assert.Null(query.ActivityTypes);
        Assert.Null(query.FromDate);
        Assert.Null(query.ToDate);
        Assert.Null(query.SearchTerm);
        Assert.Equal(100, query.Limit);
        Assert.Equal(0, query.Offset);
    }

    [Fact]
    public void AuditLogQuery_WithFilters()
    {
        var userId = UserId.New();
        var teamId = TeamId.New();
        var fromDate = DateTimeOffset.UtcNow.AddDays(-7);
        var toDate = DateTimeOffset.UtcNow;

        var query = new AuditLogQuery(
            UserId: userId,
            TeamId: teamId,
            ResourceId: null,
            ActivityTypes: new List<ActivityType>
            {
                ActivityType.TeamCreated,
                ActivityType.MemberAdded,
                ActivityType.RoleChanged
            },
            FromDate: fromDate,
            ToDate: toDate,
            SearchTerm: "created",
            Limit: 50,
            Offset: 10
        );

        Assert.Equal(userId, query.UserId);
        Assert.Equal(teamId, query.TeamId);
        Assert.Equal(3, query.ActivityTypes!.Count);
        Assert.Contains(ActivityType.TeamCreated, query.ActivityTypes);
        Assert.Equal(fromDate, query.FromDate);
        Assert.Equal(toDate, query.ToDate);
        Assert.Equal("created", query.SearchTerm);
        Assert.Equal(50, query.Limit);
        Assert.Equal(10, query.Offset);
    }

    [Fact]
    public void AuditLogQuery_WithOverride()
    {
        var originalQuery = new AuditLogQuery(
            UserId: null,
            Limit: 100
        );

        var restrictedQuery = originalQuery with { UserId = UserId.New() };

        Assert.Null(originalQuery.UserId);
        Assert.NotNull(restrictedQuery.UserId);
        Assert.Equal(100, restrictedQuery.Limit);
    }

    // ========================================================================
    // AuditLogResult Tests
    // ========================================================================

    [Fact]
    public void AuditLogResult_BasicConstruction()
    {
        var entries = new List<AuditLogEntry>
        {
            new AuditLogEntry(
                ActivityId.New(),
                UserId.New(),
                "user1",
                "User One",
                ActivityType.TeamCreated,
                "Team created",
                TeamId.New(),
                "Team A",
                null, null, null,
                DateTimeOffset.UtcNow,
                null
            ),
            new AuditLogEntry(
                ActivityId.New(),
                UserId.New(),
                "user2",
                "User Two",
                ActivityType.MemberAdded,
                "Member added",
                TeamId.New(),
                "Team A",
                null, UserId.New(), "user3",
                DateTimeOffset.UtcNow,
                null
            )
        };

        var query = new AuditLogQuery(Limit: 50);
        var result = new AuditLogResult(
            Entries: entries,
            TotalCount: 2,
            Query: query
        );

        Assert.Equal(2, result.Entries.Count);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(50, result.Query.Limit);
    }

    // ========================================================================
    // ActivityStats Tests
    // ========================================================================

    [Fact]
    public void ActivityStats_BasicConstruction()
    {
        var fromDate = DateTimeOffset.UtcNow.AddDays(-30);
        var toDate = DateTimeOffset.UtcNow;

        var activityByType = new Dictionary<ActivityType, int>
        {
            [ActivityType.TeamCreated] = 5,
            [ActivityType.MemberAdded] = 15,
            [ActivityType.ResourceShared] = 30,
            [ActivityType.CommentAdded] = 50
        };

        var mostActiveUsers = new List<UserActivityStat>
        {
            new UserActivityStat(UserId.New(), "user1", "User One", 45),
            new UserActivityStat(UserId.New(), "user2", "User Two", 30),
            new UserActivityStat(UserId.New(), "user3", "User Three", 25)
        };

        var activityByDay = new Dictionary<DateOnly, int>
        {
            [DateOnly.FromDateTime(DateTime.Today.AddDays(-2))] = 10,
            [DateOnly.FromDateTime(DateTime.Today.AddDays(-1))] = 15,
            [DateOnly.FromDateTime(DateTime.Today)] = 8
        };

        var stats = new ActivityStats(
            TotalActivities: 100,
            FromDate: fromDate,
            ToDate: toDate,
            TeamId: TeamId.New(),
            ActivityByType: activityByType,
            MostActiveUsers: mostActiveUsers,
            ActivityByDay: activityByDay
        );

        Assert.Equal(100, stats.TotalActivities);
        Assert.Equal(4, stats.ActivityByType.Count);
        Assert.Equal(50, stats.ActivityByType[ActivityType.CommentAdded]);
        Assert.Equal(3, stats.MostActiveUsers.Count);
        Assert.Equal("user1", stats.MostActiveUsers[0].Username);
        Assert.Equal(45, stats.MostActiveUsers[0].ActivityCount);
        Assert.Equal(3, stats.ActivityByDay.Count);
    }

    [Fact]
    public void ActivityStats_WithoutTeamId()
    {
        var stats = new ActivityStats(
            TotalActivities: 50,
            FromDate: DateTimeOffset.UtcNow.AddDays(-7),
            ToDate: DateTimeOffset.UtcNow,
            TeamId: null,
            ActivityByType: new Dictionary<ActivityType, int>(),
            MostActiveUsers: new List<UserActivityStat>(),
            ActivityByDay: new Dictionary<DateOnly, int>()
        );

        Assert.Null(stats.TeamId);
        Assert.Equal(50, stats.TotalActivities);
    }

    // ========================================================================
    // UserActivityStat Tests
    // ========================================================================

    [Fact]
    public void UserActivityStat_BasicConstruction()
    {
        var userId = UserId.New();

        var stat = new UserActivityStat(
            UserId: userId,
            Username: "topuser",
            DisplayName: "Top User",
            ActivityCount: 150
        );

        Assert.Equal(userId, stat.UserId);
        Assert.Equal("topuser", stat.Username);
        Assert.Equal("Top User", stat.DisplayName);
        Assert.Equal(150, stat.ActivityCount);
    }

    [Fact]
    public void UserActivityStat_Ordering()
    {
        var stats = new List<UserActivityStat>
        {
            new UserActivityStat(UserId.New(), "user1", "User One", 10),
            new UserActivityStat(UserId.New(), "user2", "User Two", 50),
            new UserActivityStat(UserId.New(), "user3", "User Three", 25)
        };

        var ordered = stats.OrderByDescending(s => s.ActivityCount).ToList();

        Assert.Equal("user2", ordered[0].Username);
        Assert.Equal("user3", ordered[1].Username);
        Assert.Equal("user1", ordered[2].Username);
    }

    // ========================================================================
    // NotificationCount Tests
    // ========================================================================

    [Fact]
    public void NotificationCount_BasicConstruction()
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
        Assert.Equal(15, count.Total);
    }

    [Fact]
    public void NotificationCount_AllUnread()
    {
        var count = new NotificationCount(Unread: 10, Total: 10);

        Assert.Equal(10, count.Unread);
        Assert.Equal(10, count.Total);
    }

    // ========================================================================
    // Activity Types Coverage Tests
    // ========================================================================

    [Theory]
    [InlineData(ActivityType.UserCreated)]
    [InlineData(ActivityType.TeamCreated)]
    [InlineData(ActivityType.TeamDeleted)]
    [InlineData(ActivityType.MemberAdded)]
    [InlineData(ActivityType.MemberRemoved)]
    [InlineData(ActivityType.RoleChanged)]
    [InlineData(ActivityType.ResourceShared)]
    [InlineData(ActivityType.ResourceUnshared)]
    [InlineData(ActivityType.PermissionChanged)]
    [InlineData(ActivityType.CommentAdded)]
    [InlineData(ActivityType.AnnotationAdded)]
    [InlineData(ActivityType.ScriptExecuted)]
    [InlineData(ActivityType.RunbookExecuted)]
    public void ActivityFeedItem_AllActivityTypes(ActivityType type)
    {
        var item = new ActivityFeedItem(
            ActivityId.New(),
            UserId.New(),
            "user",
            "User",
            type,
            $"Activity: {type}",
            null, null, null,
            DateTimeOffset.UtcNow,
            null
        );

        Assert.Equal(type, item.Type);
    }

    // ========================================================================
    // Date Range Tests
    // ========================================================================

    [Fact]
    public void AuditLogQuery_DateRange()
    {
        var fromDate = DateTimeOffset.UtcNow.AddDays(-30);
        var toDate = DateTimeOffset.UtcNow;

        var query = new AuditLogQuery(
            FromDate: fromDate,
            ToDate: toDate
        );

        Assert.Equal(fromDate, query.FromDate);
        Assert.Equal(toDate, query.ToDate);

        // Verify range is valid
        Assert.True(query.ToDate > query.FromDate);
    }

    [Fact]
    public void ActivityStats_DateRangeCalculation()
    {
        var fromDate = DateTimeOffset.UtcNow.AddDays(-7);
        var toDate = DateTimeOffset.UtcNow;

        var stats = new ActivityStats(
            TotalActivities: 100,
            FromDate: fromDate,
            ToDate: toDate,
            TeamId: null,
            ActivityByType: new Dictionary<ActivityType, int>(),
            MostActiveUsers: new List<UserActivityStat>(),
            ActivityByDay: new Dictionary<DateOnly, int>()
        );

        var daysDiff = (stats.ToDate - stats.FromDate).Days;
        Assert.Equal(7, daysDiff);
    }

    // ========================================================================
    // Pagination Tests
    // ========================================================================

    [Fact]
    public void AuditLogQuery_Pagination()
    {
        // Page 1
        var page1 = new AuditLogQuery(Limit: 20, Offset: 0);
        Assert.Equal(0, page1.Offset);
        Assert.Equal(20, page1.Limit);

        // Page 2
        var page2 = new AuditLogQuery(Limit: 20, Offset: 20);
        Assert.Equal(20, page2.Offset);

        // Page 3
        var page3 = new AuditLogQuery(Limit: 20, Offset: 40);
        Assert.Equal(40, page3.Offset);
    }

    // ========================================================================
    // Search Tests
    // ========================================================================

    [Fact]
    public void AuditLogQuery_SearchTerm()
    {
        var query = new AuditLogQuery(SearchTerm: "created team");

        Assert.Equal("created team", query.SearchTerm);
    }

    [Fact]
    public void AuditLogQuery_EmptySearchTerm()
    {
        var query = new AuditLogQuery(SearchTerm: "");

        Assert.Equal("", query.SearchTerm);
    }

    [Fact]
    public void AuditLogQuery_NullSearchTerm()
    {
        var query = new AuditLogQuery();

        Assert.Null(query.SearchTerm);
    }

    // ========================================================================
    // Activity By Day Tests
    // ========================================================================

    [Fact]
    public void ActivityStats_ActivityByDay()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var yesterday = today.AddDays(-1);
        var dayBefore = today.AddDays(-2);

        var activityByDay = new Dictionary<DateOnly, int>
        {
            [dayBefore] = 5,
            [yesterday] = 12,
            [today] = 8
        };

        var stats = new ActivityStats(
            TotalActivities: 25,
            FromDate: DateTimeOffset.UtcNow.AddDays(-3),
            ToDate: DateTimeOffset.UtcNow,
            TeamId: null,
            ActivityByType: new Dictionary<ActivityType, int>(),
            MostActiveUsers: new List<UserActivityStat>(),
            ActivityByDay: activityByDay
        );

        Assert.Equal(3, stats.ActivityByDay.Count);
        Assert.Equal(5, stats.ActivityByDay[dayBefore]);
        Assert.Equal(12, stats.ActivityByDay[yesterday]);
        Assert.Equal(8, stats.ActivityByDay[today]);

        // Verify total matches sum of days
        var sumOfDays = stats.ActivityByDay.Values.Sum();
        Assert.Equal(stats.TotalActivities, sumOfDays);
    }
}
