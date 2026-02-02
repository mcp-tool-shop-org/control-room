using ControlRoom.Domain.Model;

namespace ControlRoom.Tests.Unit.Domain;

/// <summary>
/// Unit tests for Team domain models and related types.
/// </summary>
public sealed class TeamTests
{
    // ========================================================================
    // ID Tests
    // ========================================================================

    [Fact]
    public void UserId_NewGeneratesUniqueIds()
    {
        var id1 = UserId.New();
        var id2 = UserId.New();
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void TeamId_NewGeneratesUniqueIds()
    {
        var id1 = TeamId.New();
        var id2 = TeamId.New();
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void TeamMembershipId_NewGeneratesUniqueIds()
    {
        var id1 = TeamMembershipId.New();
        var id2 = TeamMembershipId.New();
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void TeamInvitationId_NewGeneratesUniqueIds()
    {
        var id1 = TeamInvitationId.New();
        var id2 = TeamInvitationId.New();
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void SharedResourceId_NewGeneratesUniqueIds()
    {
        var id1 = SharedResourceId.New();
        var id2 = SharedResourceId.New();
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void ActivityId_NewGeneratesUniqueIds()
    {
        var id1 = ActivityId.New();
        var id2 = ActivityId.New();
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void CommentId_NewGeneratesUniqueIds()
    {
        var id1 = CommentId.New();
        var id2 = CommentId.New();
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void AnnotationId_NewGeneratesUniqueIds()
    {
        var id1 = AnnotationId.New();
        var id2 = AnnotationId.New();
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void NotificationId_NewGeneratesUniqueIds()
    {
        var id1 = NotificationId.New();
        var id2 = NotificationId.New();
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void UserId_ToStringReturnsGuidFormat()
    {
        var guid = Guid.NewGuid();
        var userId = new UserId(guid);
        Assert.Equal(guid.ToString("D"), userId.ToString());
    }

    // ========================================================================
    // Enum Tests
    // ========================================================================

    [Theory]
    [InlineData(UserRole.User, "User")]
    [InlineData(UserRole.Admin, "Admin")]
    [InlineData(UserRole.SuperAdmin, "SuperAdmin")]
    public void UserRole_AllValues(UserRole role, string expected)
    {
        Assert.Equal(expected, role.ToString());
    }

    [Theory]
    [InlineData(TeamRole.Viewer, "Viewer")]
    [InlineData(TeamRole.Member, "Member")]
    [InlineData(TeamRole.Admin, "Admin")]
    [InlineData(TeamRole.Owner, "Owner")]
    public void TeamRole_AllValues(TeamRole role, string expected)
    {
        Assert.Equal(expected, role.ToString());
    }

    [Theory]
    [InlineData(PermissionLevel.None, "None")]
    [InlineData(PermissionLevel.View, "View")]
    [InlineData(PermissionLevel.Execute, "Execute")]
    [InlineData(PermissionLevel.Edit, "Edit")]
    [InlineData(PermissionLevel.Admin, "Admin")]
    public void PermissionLevel_AllValues(PermissionLevel level, string expected)
    {
        Assert.Equal(expected, level.ToString());
    }

    [Theory]
    [InlineData(ResourceType.Script, "Script")]
    [InlineData(ResourceType.Runbook, "Runbook")]
    [InlineData(ResourceType.Dashboard, "Dashboard")]
    [InlineData(ResourceType.Alert, "Alert")]
    [InlineData(ResourceType.HealthCheck, "HealthCheck")]
    [InlineData(ResourceType.SelfHealingRule, "SelfHealingRule")]
    public void ResourceType_AllValues(ResourceType type, string expected)
    {
        Assert.Equal(expected, type.ToString());
    }

    [Theory]
    [InlineData(InvitationStatus.Pending, "Pending")]
    [InlineData(InvitationStatus.Accepted, "Accepted")]
    [InlineData(InvitationStatus.Declined, "Declined")]
    [InlineData(InvitationStatus.Expired, "Expired")]
    [InlineData(InvitationStatus.Cancelled, "Cancelled")]
    public void InvitationStatus_AllValues(InvitationStatus status, string expected)
    {
        Assert.Equal(expected, status.ToString());
    }

    [Theory]
    [InlineData(ActivityType.UserCreated, "UserCreated")]
    [InlineData(ActivityType.TeamCreated, "TeamCreated")]
    [InlineData(ActivityType.TeamDeleted, "TeamDeleted")]
    [InlineData(ActivityType.MemberAdded, "MemberAdded")]
    [InlineData(ActivityType.MemberRemoved, "MemberRemoved")]
    [InlineData(ActivityType.RoleChanged, "RoleChanged")]
    [InlineData(ActivityType.ResourceShared, "ResourceShared")]
    [InlineData(ActivityType.ResourceUnshared, "ResourceUnshared")]
    [InlineData(ActivityType.PermissionChanged, "PermissionChanged")]
    [InlineData(ActivityType.CommentAdded, "CommentAdded")]
    [InlineData(ActivityType.AnnotationAdded, "AnnotationAdded")]
    [InlineData(ActivityType.ScriptExecuted, "ScriptExecuted")]
    [InlineData(ActivityType.RunbookExecuted, "RunbookExecuted")]
    public void ActivityType_AllValues(ActivityType type, string expected)
    {
        Assert.Equal(expected, type.ToString());
    }

    [Theory]
    [InlineData(AnnotationType.Highlight, "Highlight")]
    [InlineData(AnnotationType.Comment, "Comment")]
    [InlineData(AnnotationType.Bookmark, "Bookmark")]
    [InlineData(AnnotationType.Warning, "Warning")]
    [InlineData(AnnotationType.Todo, "Todo")]
    public void AnnotationType_AllValues(AnnotationType type, string expected)
    {
        Assert.Equal(expected, type.ToString());
    }

    [Theory]
    [InlineData(NotificationType.TeamInvitation, "TeamInvitation")]
    [InlineData(NotificationType.ResourceShared, "ResourceShared")]
    [InlineData(NotificationType.MentionedInComment, "MentionedInComment")]
    [InlineData(NotificationType.CommentReply, "CommentReply")]
    [InlineData(NotificationType.AlertFired, "AlertFired")]
    [InlineData(NotificationType.RunbookCompleted, "RunbookCompleted")]
    [InlineData(NotificationType.PermissionChanged, "PermissionChanged")]
    public void NotificationType_AllValues(NotificationType type, string expected)
    {
        Assert.Equal(expected, type.ToString());
    }

    // ========================================================================
    // PermissionLevel Extension Tests
    // ========================================================================

    [Theory]
    [InlineData(PermissionLevel.None, false, false, false, false)]
    [InlineData(PermissionLevel.View, true, false, false, false)]
    [InlineData(PermissionLevel.Execute, true, true, false, false)]
    [InlineData(PermissionLevel.Edit, true, true, true, false)]
    [InlineData(PermissionLevel.Admin, true, true, true, true)]
    public void PermissionLevel_ExtensionMethods(PermissionLevel level, bool canView, bool canExecute, bool canEdit, bool canAdmin)
    {
        Assert.Equal(canView, level.CanView());
        Assert.Equal(canExecute, level.CanExecute());
        Assert.Equal(canEdit, level.CanEdit());
        Assert.Equal(canAdmin, level.CanAdmin());
    }

    [Fact]
    public void PermissionLevel_Comparison()
    {
        Assert.True(PermissionLevel.Admin > PermissionLevel.Edit);
        Assert.True(PermissionLevel.Edit > PermissionLevel.Execute);
        Assert.True(PermissionLevel.Execute > PermissionLevel.View);
        Assert.True(PermissionLevel.View > PermissionLevel.None);
    }

    // ========================================================================
    // TeamRole Extension Tests
    // ========================================================================

    [Theory]
    [InlineData(TeamRole.Viewer, true, false, false, false, false)]
    [InlineData(TeamRole.Member, true, true, true, false, false)]
    [InlineData(TeamRole.Admin, true, true, true, true, false)]
    [InlineData(TeamRole.Owner, true, true, true, true, true)]
    public void TeamRole_ExtensionMethods(TeamRole role, bool canView, bool canExecute, bool canEdit, bool canManage, bool isOwner)
    {
        Assert.Equal(canView, role.CanViewAll());
        Assert.Equal(canExecute, role.CanExecuteRuns());
        Assert.Equal(canEdit, role.CanEditResources());
        Assert.Equal(canManage, role.CanManageTeam());
        Assert.Equal(isOwner, role.IsOwner());
    }

    // ========================================================================
    // Domain Model Tests
    // ========================================================================

    [Fact]
    public void User_BasicConstruction()
    {
        var userId = UserId.New();
        var user = new User(
            userId,
            "testuser",
            "Test User",
            "test@example.com",
            UserRole.User,
            DateTimeOffset.UtcNow,
            null,
            new Dictionary<string, string> { ["theme"] = "dark" }
        );

        Assert.Equal(userId, user.Id);
        Assert.Equal("testuser", user.Username);
        Assert.Equal("Test User", user.DisplayName);
        Assert.Equal("test@example.com", user.Email);
        Assert.Equal(UserRole.User, user.Role);
        Assert.Null(user.LastLoginAt);
        Assert.Equal("dark", user.Preferences["theme"]);
    }

    [Fact]
    public void Team_BasicConstruction()
    {
        var teamId = TeamId.New();
        var ownerId = UserId.New();
        var team = new Team(
            teamId,
            "Engineering Team",
            "The engineering department",
            ownerId,
            DateTimeOffset.UtcNow,
            null,
            new Dictionary<string, string>(),
            new List<TeamMembership>()
        );

        Assert.Equal(teamId, team.Id);
        Assert.Equal("Engineering Team", team.Name);
        Assert.Equal("The engineering department", team.Description);
        Assert.Equal(ownerId, team.OwnerId);
        Assert.Empty(team.Members);
    }

    [Fact]
    public void TeamMembership_BasicConstruction()
    {
        var membershipId = TeamMembershipId.New();
        var teamId = TeamId.New();
        var userId = UserId.New();
        var addedBy = UserId.New();

        var membership = new TeamMembership(
            membershipId,
            teamId,
            userId,
            TeamRole.Member,
            addedBy,
            DateTimeOffset.UtcNow
        );

        Assert.Equal(membershipId, membership.Id);
        Assert.Equal(teamId, membership.TeamId);
        Assert.Equal(userId, membership.UserId);
        Assert.Equal(TeamRole.Member, membership.Role);
        Assert.Equal(addedBy, membership.AddedBy);
    }

    [Fact]
    public void TeamInvitation_BasicConstruction()
    {
        var invitationId = TeamInvitationId.New();
        var teamId = TeamId.New();
        var invitedBy = UserId.New();

        var invitation = new TeamInvitation(
            invitationId,
            teamId,
            "newuser@example.com",
            null,
            TeamRole.Member,
            invitedBy,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7),
            InvitationStatus.Pending
        );

        Assert.Equal(invitationId, invitation.Id);
        Assert.Equal(teamId, invitation.TeamId);
        Assert.Equal("newuser@example.com", invitation.Email);
        Assert.Null(invitation.InvitedUserId);
        Assert.Equal(TeamRole.Member, invitation.Role);
        Assert.Equal(InvitationStatus.Pending, invitation.Status);
    }

    [Fact]
    public void TeamInvitation_WithExistingUser()
    {
        var invitedUserId = UserId.New();
        var invitation = new TeamInvitation(
            TeamInvitationId.New(),
            TeamId.New(),
            "existing@example.com",
            invitedUserId,
            TeamRole.Admin,
            UserId.New(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7),
            InvitationStatus.Pending
        );

        Assert.Equal(invitedUserId, invitation.InvitedUserId);
        Assert.Equal(TeamRole.Admin, invitation.Role);
    }

    [Fact]
    public void SharedResource_WithTeam()
    {
        var resourceId = SharedResourceId.New();
        var ownerId = UserId.New();
        var teamId = TeamId.New();

        var shared = new SharedResource(
            resourceId,
            ResourceType.Script,
            Guid.NewGuid(),
            ownerId,
            teamId,
            null,
            DateTimeOffset.UtcNow,
            new List<ResourcePermission>()
        );

        Assert.Equal(resourceId, shared.Id);
        Assert.Equal(ResourceType.Script, shared.ResourceType);
        Assert.Equal(ownerId, shared.OwnerId);
        Assert.Equal(teamId, shared.SharedWithTeamId);
        Assert.Null(shared.SharedWithUserId);
    }

    [Fact]
    public void SharedResource_WithUser()
    {
        var userId = UserId.New();
        var shared = new SharedResource(
            SharedResourceId.New(),
            ResourceType.Dashboard,
            Guid.NewGuid(),
            UserId.New(),
            null,
            userId,
            DateTimeOffset.UtcNow,
            new List<ResourcePermission>()
        );

        Assert.Null(shared.SharedWithTeamId);
        Assert.Equal(userId, shared.SharedWithUserId);
    }

    [Fact]
    public void ResourcePermission_BasicConstruction()
    {
        var userId = UserId.New();
        var grantedBy = UserId.New();

        var permission = new ResourcePermission(
            userId,
            PermissionLevel.Edit,
            DateTimeOffset.UtcNow,
            grantedBy
        );

        Assert.Equal(userId, permission.UserId);
        Assert.Equal(PermissionLevel.Edit, permission.Level);
        Assert.Equal(grantedBy, permission.GrantedBy);
    }

    [Fact]
    public void ActivityEntry_BasicConstruction()
    {
        var activityId = ActivityId.New();
        var userId = UserId.New();

        var entry = new ActivityEntry(
            activityId,
            userId,
            ActivityType.TeamCreated,
            "Created team Engineering",
            TeamId.New(),
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            new Dictionary<string, object> { ["teamName"] = "Engineering" }
        );

        Assert.Equal(activityId, entry.Id);
        Assert.Equal(userId, entry.UserId);
        Assert.Equal(ActivityType.TeamCreated, entry.Type);
        Assert.Equal("Created team Engineering", entry.Description);
        Assert.NotNull(entry.TeamId);
        Assert.Null(entry.ResourceId);
    }

    [Fact]
    public void Comment_BasicConstruction()
    {
        var commentId = CommentId.New();
        var resourceId = SharedResourceId.New();
        var authorId = UserId.New();

        var comment = new Comment(
            commentId,
            resourceId,
            authorId,
            "This looks great!",
            DateTimeOffset.UtcNow,
            null,
            null,
            new List<UserId>(),
            new Dictionary<string, int>()
        );

        Assert.Equal(commentId, comment.Id);
        Assert.Equal(resourceId, comment.ResourceId);
        Assert.Equal(authorId, comment.AuthorId);
        Assert.Equal("This looks great!", comment.Content);
        Assert.Null(comment.ParentId);
    }

    [Fact]
    public void Comment_WithMentionsAndReactions()
    {
        var mention1 = UserId.New();
        var mention2 = UserId.New();

        var comment = new Comment(
            CommentId.New(),
            SharedResourceId.New(),
            UserId.New(),
            "Hey @user1 and @user2, check this out!",
            DateTimeOffset.UtcNow,
            null,
            null,
            new List<UserId> { mention1, mention2 },
            new Dictionary<string, int> { ["\U0001F44D"] = 3, ["\u2764\uFE0F"] = 1 }
        );

        Assert.Equal(2, comment.Mentions.Count);
        Assert.Contains(mention1, comment.Mentions);
        Assert.Equal(2, comment.Reactions.Count);
        Assert.Equal(3, comment.Reactions["\U0001F44D"]);
    }

    [Fact]
    public void Comment_Reply()
    {
        var parentId = CommentId.New();

        var reply = new Comment(
            CommentId.New(),
            SharedResourceId.New(),
            UserId.New(),
            "I agree!",
            DateTimeOffset.UtcNow,
            null,
            parentId,
            new List<UserId>(),
            new Dictionary<string, int>()
        );

        Assert.Equal(parentId, reply.ParentId);
    }

    [Fact]
    public void Annotation_BasicConstruction()
    {
        var annotationId = AnnotationId.New();
        var resourceId = SharedResourceId.New();
        var authorId = UserId.New();

        var annotation = new Annotation(
            annotationId,
            resourceId,
            authorId,
            AnnotationType.Highlight,
            "Important section",
            100,
            50,
            DateTimeOffset.UtcNow,
            null,
            "#FFFF00",
            new Dictionary<string, object> { ["importance"] = "high" }
        );

        Assert.Equal(annotationId, annotation.Id);
        Assert.Equal(resourceId, annotation.ResourceId);
        Assert.Equal(authorId, annotation.AuthorId);
        Assert.Equal(AnnotationType.Highlight, annotation.Type);
        Assert.Equal("Important section", annotation.Content);
        Assert.Equal(100, annotation.StartPosition);
        Assert.Equal(50, annotation.Length);
        Assert.Equal("#FFFF00", annotation.Color);
    }

    [Fact]
    public void Notification_BasicConstruction()
    {
        var notificationId = NotificationId.New();
        var userId = UserId.New();

        var notification = new Notification(
            notificationId,
            userId,
            NotificationType.TeamInvitation,
            "You've been invited to join Engineering Team",
            false,
            TeamId.New(),
            null,
            null,
            DateTimeOffset.UtcNow,
            null,
            null
        );

        Assert.Equal(notificationId, notification.Id);
        Assert.Equal(userId, notification.UserId);
        Assert.Equal(NotificationType.TeamInvitation, notification.Type);
        Assert.False(notification.IsRead);
        Assert.Null(notification.ReadAt);
    }

    [Fact]
    public void Notification_MarkAsRead()
    {
        var notification = new Notification(
            NotificationId.New(),
            UserId.New(),
            NotificationType.AlertFired,
            "CPU usage exceeded threshold",
            false,
            null,
            SharedResourceId.New(),
            null,
            DateTimeOffset.UtcNow,
            null,
            null
        );

        var readNotification = notification with
        {
            IsRead = true,
            ReadAt = DateTimeOffset.UtcNow
        };

        Assert.True(readNotification.IsRead);
        Assert.NotNull(readNotification.ReadAt);
    }

    [Fact]
    public void Team_WithMembers()
    {
        var teamId = TeamId.New();
        var ownerId = UserId.New();
        var memberId = UserId.New();

        var membership = new TeamMembership(
            TeamMembershipId.New(),
            teamId,
            memberId,
            TeamRole.Member,
            ownerId,
            DateTimeOffset.UtcNow
        );

        var team = new Team(
            teamId,
            "Test Team",
            "Description",
            ownerId,
            DateTimeOffset.UtcNow,
            null,
            new Dictionary<string, string>(),
            new List<TeamMembership> { membership }
        );

        Assert.Single(team.Members);
        Assert.Equal(memberId, team.Members[0].UserId);
    }

    [Fact]
    public void SharedResource_WithPermissions()
    {
        var ownerId = UserId.New();
        var user1 = UserId.New();
        var user2 = UserId.New();

        var permissions = new List<ResourcePermission>
        {
            new(ownerId, PermissionLevel.Admin, DateTimeOffset.UtcNow, ownerId),
            new(user1, PermissionLevel.Edit, DateTimeOffset.UtcNow, ownerId),
            new(user2, PermissionLevel.View, DateTimeOffset.UtcNow, ownerId)
        };

        var shared = new SharedResource(
            SharedResourceId.New(),
            ResourceType.Runbook,
            Guid.NewGuid(),
            ownerId,
            TeamId.New(),
            null,
            DateTimeOffset.UtcNow,
            permissions
        );

        Assert.Equal(3, shared.Permissions.Count);
        Assert.Equal(PermissionLevel.Admin, shared.Permissions.First(p => p.UserId == ownerId).Level);
        Assert.Equal(PermissionLevel.Edit, shared.Permissions.First(p => p.UserId == user1).Level);
        Assert.Equal(PermissionLevel.View, shared.Permissions.First(p => p.UserId == user2).Level);
    }
}
