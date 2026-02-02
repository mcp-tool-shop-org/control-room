namespace ControlRoom.Domain.Model;

// ============================================================================
// Team Collaboration IDs
// ============================================================================

public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public readonly record struct TeamId(Guid Value)
{
    public static TeamId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public readonly record struct TeamMembershipId(Guid Value)
{
    public static TeamMembershipId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public readonly record struct TeamInvitationId(Guid Value)
{
    public static TeamInvitationId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public readonly record struct SharedResourceId(Guid Value)
{
    public static SharedResourceId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public readonly record struct ActivityId(Guid Value)
{
    public static ActivityId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public readonly record struct CommentId(Guid Value)
{
    public static CommentId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public readonly record struct AnnotationId(Guid Value)
{
    public static AnnotationId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public readonly record struct NotificationId(Guid Value)
{
    public static NotificationId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

// ============================================================================
// Team Enums
// ============================================================================

/// <summary>
/// User role within the system.
/// </summary>
public enum UserRole
{
    User = 1,
    Admin = 2,
    SuperAdmin = 3
}

/// <summary>
/// User role within a team.
/// </summary>
public enum TeamRole
{
    Viewer = 1,
    Member = 2,
    Admin = 3,
    Owner = 4
}

/// <summary>
/// Permission level for shared resources.
/// </summary>
public enum PermissionLevel
{
    None = 0,
    View = 1,
    Execute = 2,
    Edit = 3,
    Admin = 4
}

/// <summary>
/// Type of resource that can be shared.
/// </summary>
public enum ResourceType
{
    Script = 1,
    Runbook = 2,
    Dashboard = 3,
    Alert = 4,
    HealthCheck = 5,
    SelfHealingRule = 6
}

/// <summary>
/// Status of a team invitation.
/// </summary>
public enum InvitationStatus
{
    Pending = 1,
    Accepted = 2,
    Declined = 3,
    Expired = 4,
    Cancelled = 5
}

/// <summary>
/// Type of activity for audit logging.
/// </summary>
public enum ActivityType
{
    UserCreated = 1,
    TeamCreated = 2,
    TeamDeleted = 3,
    MemberAdded = 4,
    MemberRemoved = 5,
    RoleChanged = 6,
    ResourceShared = 7,
    ResourceUnshared = 8,
    PermissionChanged = 9,
    CommentAdded = 10,
    AnnotationAdded = 11,
    ScriptExecuted = 12,
    RunbookExecuted = 13
}

/// <summary>
/// Type of annotation.
/// </summary>
public enum AnnotationType
{
    Highlight = 1,
    Comment = 2,
    Bookmark = 3,
    Warning = 4,
    Todo = 5
}

/// <summary>
/// Type of notification.
/// </summary>
public enum NotificationType
{
    TeamInvitation = 1,
    ResourceShared = 2,
    MentionedInComment = 3,
    CommentReply = 4,
    AlertFired = 5,
    RunbookCompleted = 6,
    PermissionChanged = 7
}

// ============================================================================
// Domain Models
// ============================================================================

/// <summary>
/// Represents a user in the system.
/// </summary>
public sealed record User(
    UserId Id,
    string Username,
    string DisplayName,
    string Email,
    UserRole Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    Dictionary<string, string> Preferences
);

/// <summary>
/// Represents a team for collaboration.
/// </summary>
public sealed record Team(
    TeamId Id,
    string Name,
    string Description,
    UserId OwnerId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Dictionary<string, string> Settings,
    List<TeamMembership> Members
);

/// <summary>
/// Represents membership of a user in a team.
/// </summary>
public sealed record TeamMembership(
    TeamMembershipId Id,
    TeamId TeamId,
    UserId UserId,
    TeamRole Role,
    UserId AddedBy,
    DateTimeOffset JoinedAt
);

/// <summary>
/// Represents an invitation to join a team.
/// </summary>
public sealed record TeamInvitation(
    TeamInvitationId Id,
    TeamId TeamId,
    string Email,
    UserId? InvitedUserId,
    TeamRole Role,
    UserId InvitedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    InvitationStatus Status
);

/// <summary>
/// Represents a shared resource with permissions.
/// </summary>
public sealed record SharedResource(
    SharedResourceId Id,
    ResourceType ResourceType,
    Guid ResourceId,
    UserId OwnerId,
    TeamId? SharedWithTeamId,
    UserId? SharedWithUserId,
    DateTimeOffset SharedAt,
    List<ResourcePermission> Permissions
);

/// <summary>
/// Represents a permission grant on a shared resource.
/// </summary>
public sealed record ResourcePermission(
    UserId UserId,
    PermissionLevel Level,
    DateTimeOffset GrantedAt,
    UserId GrantedBy
);

/// <summary>
/// Represents an activity entry for audit logging.
/// </summary>
public sealed record ActivityEntry(
    ActivityId Id,
    UserId UserId,
    ActivityType Type,
    string Description,
    TeamId? TeamId,
    SharedResourceId? ResourceId,
    UserId? TargetUserId,
    CommentId? CommentId,
    DateTimeOffset OccurredAt,
    Dictionary<string, object>? Metadata
);

/// <summary>
/// Represents a comment on a shared resource.
/// </summary>
public sealed record Comment(
    CommentId Id,
    SharedResourceId ResourceId,
    UserId AuthorId,
    string Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EditedAt,
    CommentId? ParentId,
    List<UserId> Mentions,
    Dictionary<string, int> Reactions
);

/// <summary>
/// Represents an annotation on a resource (highlight, bookmark, etc.).
/// </summary>
public sealed record Annotation(
    AnnotationId Id,
    SharedResourceId ResourceId,
    UserId AuthorId,
    AnnotationType Type,
    string Content,
    int StartPosition,
    int Length,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? Color,
    Dictionary<string, object>? Metadata
);

/// <summary>
/// Represents a notification to a user.
/// </summary>
public sealed record Notification(
    NotificationId Id,
    UserId UserId,
    NotificationType Type,
    string Message,
    bool IsRead,
    TeamId? TeamId,
    SharedResourceId? ResourceId,
    CommentId? CommentId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt,
    Dictionary<string, object>? Metadata
);

// ============================================================================
// Extension Methods
// ============================================================================

/// <summary>
/// Extension methods for PermissionLevel.
/// </summary>
public static class PermissionLevelExtensions
{
    public static bool CanView(this PermissionLevel level) => level >= PermissionLevel.View;
    public static bool CanExecute(this PermissionLevel level) => level >= PermissionLevel.Execute;
    public static bool CanEdit(this PermissionLevel level) => level >= PermissionLevel.Edit;
    public static bool CanAdmin(this PermissionLevel level) => level >= PermissionLevel.Admin;
}

/// <summary>
/// Extension methods for TeamRole.
/// </summary>
public static class TeamRoleExtensions
{
    public static bool CanViewAll(this TeamRole role) => role >= TeamRole.Viewer;
    public static bool CanExecuteRuns(this TeamRole role) => role >= TeamRole.Member;
    public static bool CanEditResources(this TeamRole role) => role >= TeamRole.Member;
    public static bool CanManageTeam(this TeamRole role) => role >= TeamRole.Admin;
    public static bool IsOwner(this TeamRole role) => role == TeamRole.Owner;
}
