using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlRoom.Application.UseCases;
using ControlRoom.Domain.Model;

namespace ControlRoom.App.ViewModels;

/// <summary>
/// ViewModel for the Team Dashboard page.
/// </summary>
public partial class TeamDashboardViewModel : ObservableObject
{
    private readonly TeamManagement _teamManagement;
    private readonly ResourceSharing _resourceSharing;
    private readonly ActivityFeed _activityFeed;
    private readonly Collaboration? _collaboration;

    public TeamDashboardViewModel(
        TeamManagement teamManagement,
        ResourceSharing resourceSharing,
        ActivityFeed activityFeed,
        Collaboration? collaboration = null)
    {
        _teamManagement = teamManagement;
        _resourceSharing = resourceSharing;
        _activityFeed = activityFeed;
        _collaboration = collaboration;

        // Subscribe to events
        _teamManagement.TeamCreated += OnTeamCreated;
        _teamManagement.MemberAdded += OnMemberAdded;
        if (_collaboration is not null)
        {
            _collaboration.CommentAdded += OnCommentAdded;
        }
    }

    public ObservableCollection<TeamViewModel> Teams { get; } = [];
    public ObservableCollection<SharedResourceViewModel> SharedWithMe { get; } = [];
    public ObservableCollection<ActivityFeedItemViewModel> RecentActivity { get; } = [];
    public ObservableCollection<NotificationViewModel> Notifications { get; } = [];
    public ObservableCollection<TeamInvitationViewModel> PendingInvitations { get; } = [];

    [ObservableProperty]
    private UserViewModel? currentUser;

    [ObservableProperty]
    private TeamViewModel? selectedTeam;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private int unreadNotificationCount;

    [ObservableProperty]
    private string searchQuery = "";

    partial void OnSelectedTeamChanged(TeamViewModel? value)
    {
        if (value is not null)
        {
            _ = LoadTeamDetailsAsync(value.Id);
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;

        await Task.Run(() =>
        {
            var user = _teamManagement.GetCurrentUser();
            var teams = _teamManagement.GetUserTeams();
            var sharedResources = _resourceSharing.GetResourcesSharedWithMe();
            var activity = _activityFeed.GetFeed(20);
            var invitations = _teamManagement.GetUserInvitations();
            var notificationCount = _activityFeed.GetNotificationCount();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                CurrentUser = new UserViewModel(user);

                Teams.Clear();
                foreach (var team in teams)
                {
                    Teams.Add(new TeamViewModel(team));
                }

                SharedWithMe.Clear();
                foreach (var resource in sharedResources.Take(10))
                {
                    SharedWithMe.Add(new SharedResourceViewModel(resource));
                }

                RecentActivity.Clear();
                foreach (var item in activity)
                {
                    RecentActivity.Add(new ActivityFeedItemViewModel(item));
                }

                PendingInvitations.Clear();
                foreach (var invitation in invitations)
                {
                    PendingInvitations.Add(new TeamInvitationViewModel(invitation));
                }

                UnreadNotificationCount = notificationCount.Unread;
            });
        });

        IsLoading = false;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private async Task CreateTeamAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        await Task.Run(() =>
        {
            var team = _teamManagement.CreateTeam(name);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Teams.Insert(0, new TeamViewModel(team));
            });
        });
    }

    [RelayCommand]
    private async Task AcceptInvitationAsync(TeamInvitationViewModel? invitation)
    {
        if (invitation is null) return;

        await Task.Run(() =>
        {
            _teamManagement.AcceptInvitation(invitation.Id);
        });

        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeclineInvitationAsync(TeamInvitationViewModel? invitation)
    {
        if (invitation is null) return;

        await Task.Run(() =>
        {
            _teamManagement.DeclineInvitation(invitation.Id);
        });

        PendingInvitations.Remove(invitation);
    }

    [RelayCommand]
    private async Task MarkAllNotificationsReadAsync()
    {
        await Task.Run(() =>
        {
            _activityFeed.MarkAllNotificationsRead();
        });

        UnreadNotificationCount = 0;
    }

    [RelayCommand]
    private async Task LoadTeamDetailsAsync(TeamId teamId)
    {
        var team = _teamManagement.GetTeam(teamId);
        if (team is not null)
        {
            SelectedTeam = new TeamViewModel(team);
        }
    }

    [RelayCommand]
    private async Task LeaveTeamAsync(TeamViewModel? team)
    {
        if (team is null || CurrentUser is null) return;

        await Task.Run(() =>
        {
            _teamManagement.RemoveMember(team.Id, CurrentUser.Id);
        });

        Teams.Remove(team);
        if (SelectedTeam?.Id == team.Id)
        {
            SelectedTeam = null;
        }
    }

    private void OnTeamCreated(object? sender, TeamCreatedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Teams.Insert(0, new TeamViewModel(e.Team));
        });
    }

    private void OnMemberAdded(object? sender, MemberAddedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await LoadAsync();
        });
    }

    private void OnCommentAdded(object? sender, CommentAddedEventArgs e)
    {
        // Refresh activity feed when new comments are added
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var activity = await Task.Run(() => _activityFeed.GetFeed(20));
            RecentActivity.Clear();
            foreach (var item in activity)
            {
                RecentActivity.Add(new ActivityFeedItemViewModel(item));
            }
        });
    }

    public void Cleanup()
    {
        _teamManagement.TeamCreated -= OnTeamCreated;
        _teamManagement.MemberAdded -= OnMemberAdded;
        if (_collaboration is not null)
        {
            _collaboration.CommentAdded -= OnCommentAdded;
        }
    }
}

// ============================================================================
// View Models for UI Display
// ============================================================================

public partial class UserViewModel : ObservableObject
{
    private readonly User _user;

    public UserViewModel(User user)
    {
        _user = user;
    }

    public UserId Id => _user.Id;
    public string Username => _user.Username;
    public string DisplayName => _user.DisplayName;
    public string Email => _user.Email;
    public UserRole Role => _user.Role;
    public string Initials => GetInitials(_user.DisplayName);

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{parts[0][0]}{parts[1][0]}".ToUpper();
        if (parts.Length == 1 && parts[0].Length >= 2)
            return parts[0][..2].ToUpper();
        return name.Length >= 1 ? name[0].ToString().ToUpper() : "?";
    }
}

public partial class TeamViewModel : ObservableObject
{
    private readonly Team _team;

    public TeamViewModel(Team team)
    {
        _team = team;
        Members = team.Members.Select(m => new TeamMemberViewModel(m)).ToList();
    }

    public TeamId Id => _team.Id;
    public string Name => _team.Name;
    public string Description => _team.Description;
    public UserId OwnerId => _team.OwnerId;
    public DateTimeOffset CreatedAt => _team.CreatedAt;
    public int MemberCount => _team.Members.Count;
    public List<TeamMemberViewModel> Members { get; }

    public string MemberCountText => MemberCount == 1 ? "1 member" : $"{MemberCount} members";
    public string CreatedAtText => GetRelativeTime(_team.CreatedAt);

    private static string GetRelativeTime(DateTimeOffset time)
    {
        var diff = DateTimeOffset.UtcNow - time;
        if (diff.TotalDays >= 365) return $"{(int)(diff.TotalDays / 365)}y ago";
        if (diff.TotalDays >= 30) return $"{(int)(diff.TotalDays / 30)}mo ago";
        if (diff.TotalDays >= 7) return $"{(int)(diff.TotalDays / 7)}w ago";
        if (diff.TotalDays >= 1) return $"{(int)diff.TotalDays}d ago";
        if (diff.TotalHours >= 1) return $"{(int)diff.TotalHours}h ago";
        return "just now";
    }
}

public partial class TeamMemberViewModel : ObservableObject
{
    private readonly TeamMembership _membership;

    public TeamMemberViewModel(TeamMembership membership)
    {
        _membership = membership;
    }

    public UserId UserId => _membership.UserId;
    public TeamRole Role => _membership.Role;
    public string RoleText => _membership.Role.ToString();
    public DateTimeOffset JoinedAt => _membership.JoinedAt;

    public string RoleColor => _membership.Role switch
    {
        TeamRole.Owner => "#D32F2F",
        TeamRole.Admin => "#1976D2",
        TeamRole.Member => "#388E3C",
        TeamRole.Viewer => "#757575",
        _ => "#9E9E9E"
    };
}

public partial class SharedResourceViewModel : ObservableObject
{
    private readonly SharedResource _resource;

    public SharedResourceViewModel(SharedResource resource)
    {
        _resource = resource;
    }

    public SharedResourceId Id => _resource.Id;
    public ResourceType ResourceType => _resource.ResourceType;
    public string ResourceTypeText => _resource.ResourceType.ToString();
    public UserId OwnerId => _resource.OwnerId;
    public DateTimeOffset SharedAt => _resource.SharedAt;

    public string Icon => _resource.ResourceType switch
    {
        ResourceType.Script => "\U0001F4DC",
        ResourceType.Runbook => "\U0001F4D6",
        ResourceType.Dashboard => "\U0001F4CA",
        ResourceType.Alert => "\U0001F514",
        ResourceType.HealthCheck => "\u2764\uFE0F",
        ResourceType.SelfHealingRule => "\U0001F527",
        _ => "\U0001F4C4"
    };

    public string SharedAtText => GetRelativeTime(_resource.SharedAt);

    private static string GetRelativeTime(DateTimeOffset time)
    {
        var diff = DateTimeOffset.UtcNow - time;
        if (diff.TotalDays >= 1) return $"{(int)diff.TotalDays}d ago";
        if (diff.TotalHours >= 1) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalMinutes >= 1) return $"{(int)diff.TotalMinutes}m ago";
        return "just now";
    }
}

public partial class ActivityFeedItemViewModel : ObservableObject
{
    private readonly ActivityFeedItem _item;

    public ActivityFeedItemViewModel(ActivityFeedItem item)
    {
        _item = item;
    }

    public ActivityId Id => _item.Id;
    public string Username => _item.Username;
    public string DisplayName => _item.DisplayName;
    public ActivityType Type => _item.Type;
    public string Description => _item.Description;
    public DateTimeOffset OccurredAt => _item.OccurredAt;

    public string Icon => _item.Type switch
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

    public string OccurredAtText => GetRelativeTime(_item.OccurredAt);
    public string Initials => GetInitials(_item.DisplayName);

    private static string GetRelativeTime(DateTimeOffset time)
    {
        var diff = DateTimeOffset.UtcNow - time;
        if (diff.TotalDays >= 1) return $"{(int)diff.TotalDays}d ago";
        if (diff.TotalHours >= 1) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalMinutes >= 1) return $"{(int)diff.TotalMinutes}m ago";
        return "just now";
    }

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{parts[0][0]}{parts[1][0]}".ToUpper();
        return name.Length >= 1 ? name[0].ToString().ToUpper() : "?";
    }
}

public partial class NotificationViewModel : ObservableObject
{
    private readonly Notification _notification;

    public NotificationViewModel(Notification notification)
    {
        _notification = notification;
    }

    public NotificationId Id => _notification.Id;
    public NotificationType Type => _notification.Type;
    public string Message => _notification.Message;
    public bool IsRead => _notification.IsRead;
    public DateTimeOffset CreatedAt => _notification.CreatedAt;

    public string Icon => _notification.Type switch
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

    public string CreatedAtText => GetRelativeTime(_notification.CreatedAt);

    private static string GetRelativeTime(DateTimeOffset time)
    {
        var diff = DateTimeOffset.UtcNow - time;
        if (diff.TotalDays >= 1) return $"{(int)diff.TotalDays}d ago";
        if (diff.TotalHours >= 1) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalMinutes >= 1) return $"{(int)diff.TotalMinutes}m ago";
        return "just now";
    }
}

public partial class TeamInvitationViewModel : ObservableObject
{
    private readonly TeamInvitation _invitation;

    public TeamInvitationViewModel(TeamInvitation invitation)
    {
        _invitation = invitation;
    }

    public TeamInvitationId Id => _invitation.Id;
    public TeamId TeamId => _invitation.TeamId;
    public TeamRole Role => _invitation.Role;
    public string RoleText => _invitation.Role.ToString();
    public DateTimeOffset CreatedAt => _invitation.CreatedAt;
    public DateTimeOffset ExpiresAt => _invitation.ExpiresAt;
    public InvitationStatus Status => _invitation.Status;

    public string CreatedAtText => GetRelativeTime(_invitation.CreatedAt);
    public bool IsExpired => _invitation.ExpiresAt < DateTimeOffset.UtcNow;
    public string ExpiresInText => GetExpiresInText(_invitation.ExpiresAt);

    private static string GetRelativeTime(DateTimeOffset time)
    {
        var diff = DateTimeOffset.UtcNow - time;
        if (diff.TotalDays >= 1) return $"{(int)diff.TotalDays}d ago";
        if (diff.TotalHours >= 1) return $"{(int)diff.TotalHours}h ago";
        return "just now";
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
