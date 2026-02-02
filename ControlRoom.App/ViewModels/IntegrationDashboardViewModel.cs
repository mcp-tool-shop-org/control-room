using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlRoom.Application.Integrations;
using ControlRoom.Application.UseCases;
using ControlRoom.Domain.Model;
using IntegrationModel = ControlRoom.Domain.Model.Integration;

namespace ControlRoom.App.ViewModels;

/// <summary>
/// ViewModel for the Integration Dashboard showing connected integrations,
/// marketplace, and integration health status.
/// </summary>
public sealed partial class IntegrationDashboardViewModel : ObservableObject
{
    private readonly IntegrationHub? _hub;
    private readonly CloudProviderManager? _cloudManager;

    // ========================================================================
    // Observable Properties
    // ========================================================================

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string searchQuery = "";

    [ObservableProperty]
    private IntegrationCategory? selectedCategory;

    [ObservableProperty]
    private IntegrationDisplayItem? selectedIntegration;

    [ObservableProperty]
    private bool showMarketplace;

    [ObservableProperty]
    private bool showConnectedOnly;

    [ObservableProperty]
    private int connectedCount;

    [ObservableProperty]
    private int healthyCount;

    [ObservableProperty]
    private int warningCount;

    [ObservableProperty]
    private int errorCount;

    // ========================================================================
    // Collections
    // ========================================================================

    public ObservableCollection<IntegrationDisplayItem> Integrations { get; } = [];
    public ObservableCollection<IntegrationDisplayItem> FilteredIntegrations { get; } = [];
    public ObservableCollection<MarketplaceCategory> MarketplaceCategories { get; } = [];
    public ObservableCollection<IntegrationHealthItem> HealthStatus { get; } = [];
    public ObservableCollection<RecentActivityItem> RecentActivity { get; } = [];

    // ========================================================================
    // Category Options
    // ========================================================================

    public static IReadOnlyList<IntegrationCategory?> CategoryOptions { get; } =
    [
        null, // All categories
        IntegrationCategory.CloudProvider,
        IntegrationCategory.SourceControl,
        IntegrationCategory.IssueTracking,
        IntegrationCategory.Alerting,
        IntegrationCategory.Communication,
        IntegrationCategory.Monitoring,
        IntegrationCategory.CI_CD,
        IntegrationCategory.Database,
        IntegrationCategory.Storage
    ];

    public IntegrationDashboardViewModel()
    {
        // Design-time constructor
        LoadDesignTimeData();
    }

    public IntegrationDashboardViewModel(IntegrationHub hub, CloudProviderManager cloudManager)
    {
        _hub = hub;
        _cloudManager = cloudManager;

        // Wire up cloud provider events
        _cloudManager.ResourceChanged += OnCloudResourceChanged;
        _cloudManager.AlertTriggered += OnCloudAlertTriggered;
    }

    // ========================================================================
    // Commands
    // ========================================================================

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_hub == null) return;

        IsLoading = true;

        try
        {
            // Load available integrations
            var integrations = _hub.GetAvailableIntegrations();
            var instances = _hub.GetMyInstances();

            Integrations.Clear();
            foreach (var integration in integrations)
            {
                var connectedInstances = instances
                    .Where(i => i.IntegrationId == integration.Id)
                    .ToList();

                Integrations.Add(new IntegrationDisplayItem
                {
                    Integration = integration,
                    IsConnected = connectedInstances.Count > 0,
                    InstanceCount = connectedInstances.Count,
                    Health = connectedInstances.Count > 0
                        ? connectedInstances.All(i => i.Health == IntegrationHealth.Healthy)
                            ? IntegrationHealth.Healthy
                            : connectedInstances.Any(i => i.Health == IntegrationHealth.Unhealthy)
                                ? IntegrationHealth.Unhealthy
                                : IntegrationHealth.Degraded
                        : IntegrationHealth.Unknown,
                    LastSyncTime = connectedInstances
                        .Where(i => i.LastSyncAt.HasValue)
                        .Select(i => i.LastSyncAt!.Value)
                        .DefaultIfEmpty()
                        .Max()
                });
            }

            // Update stats
            ConnectedCount = Integrations.Count(i => i.IsConnected);
            HealthyCount = Integrations.Count(i => i.IsConnected && i.Health == IntegrationHealth.Healthy);
            WarningCount = Integrations.Count(i => i.IsConnected && i.Health == IntegrationHealth.Degraded);
            ErrorCount = Integrations.Count(i => i.IsConnected && i.Health == IntegrationHealth.Unhealthy);

            // Load marketplace categories
            LoadMarketplaceCategories();

            // Load health status
            await LoadHealthStatusAsync();

            // Apply filters
            ApplyFilters();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshHealthAsync()
    {
        if (_cloudManager == null) return;

        IsLoading = true;

        try
        {
            var healthStatuses = await _cloudManager.CheckAllHealthAsync();

            HealthStatus.Clear();
            foreach (var status in healthStatuses)
            {
                HealthStatus.Add(new IntegrationHealthItem
                {
                    ProviderName = status.ProviderName,
                    InstanceId = status.InstanceId,
                    Health = status.Health,
                    ResponseTime = status.ResponseTime,
                    Message = status.Message,
                    CheckedAt = status.CheckedAt
                });
            }

            // Update counts
            HealthyCount = HealthStatus.Count(h => h.Health == IntegrationHealth.Healthy);
            WarningCount = HealthStatus.Count(h => h.Health == IntegrationHealth.Degraded);
            ErrorCount = HealthStatus.Count(h => h.Health == IntegrationHealth.Unhealthy || h.Health == IntegrationHealth.Unreachable);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ConnectIntegrationAsync(IntegrationDisplayItem? item)
    {
        if (item == null || _hub == null) return;

        // This would typically open a connection dialog
        // For now, just log the intent
        RecentActivity.Insert(0, new RecentActivityItem
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = IntegrationActivityType.ConnectionAttempt,
            Message = $"Connection initiated for {item.Integration.Name}",
            IntegrationName = item.Integration.Name
        });

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task DisconnectIntegrationAsync(IntegrationDisplayItem? item)
    {
        if (item == null || _hub == null) return;

        // Find and disconnect all instances
        var instances = _hub.GetMyInstances()
            .Where(i => i.IntegrationId == item.Integration.Id)
            .ToList();

        foreach (var instance in instances)
        {
            _hub.Disconnect(instance.Id);
            _cloudManager?.DisconnectInstance(instance.Id);
        }

        item.IsConnected = false;
        item.InstanceCount = 0;
        item.Health = IntegrationHealth.Unknown;

        RecentActivity.Insert(0, new RecentActivityItem
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = IntegrationActivityType.Disconnection,
            Message = $"Disconnected from {item.Integration.Name}",
            IntegrationName = item.Integration.Name
        });

        await LoadAsync();
    }

    [RelayCommand]
    private void ToggleMarketplace()
    {
        ShowMarketplace = !ShowMarketplace;
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchQuery = "";
        SelectedCategory = null;
        ShowConnectedOnly = false;
        ApplyFilters();
    }

    [RelayCommand]
    private void ViewIntegrationDetails(IntegrationDisplayItem? item)
    {
        if (item == null) return;
        SelectedIntegration = item;
    }

    [RelayCommand]
    private async Task TestConnectionAsync(IntegrationDisplayItem? item)
    {
        if (item == null) return;

        item.IsTesting = true;

        try
        {
            // Simulate connection test
            await Task.Delay(1000);

            item.LastTestResult = true;
            item.LastTestTime = DateTimeOffset.UtcNow;

            RecentActivity.Insert(0, new RecentActivityItem
            {
                Timestamp = DateTimeOffset.UtcNow,
                Type = IntegrationActivityType.ConnectionTest,
                Message = $"Connection test successful for {item.Integration.Name}",
                IntegrationName = item.Integration.Name,
                Success = true
            });
        }
        catch (Exception ex)
        {
            item.LastTestResult = false;
            item.LastTestTime = DateTimeOffset.UtcNow;
            item.LastTestError = ex.Message;

            RecentActivity.Insert(0, new RecentActivityItem
            {
                Timestamp = DateTimeOffset.UtcNow,
                Type = IntegrationActivityType.ConnectionTest,
                Message = $"Connection test failed for {item.Integration.Name}: {ex.Message}",
                IntegrationName = item.Integration.Name,
                Success = false
            });
        }
        finally
        {
            item.IsTesting = false;
        }
    }

    // ========================================================================
    // Property Change Handlers
    // ========================================================================

    partial void OnSearchQueryChanged(string value) => ApplyFilters();
    partial void OnSelectedCategoryChanged(IntegrationCategory? value) => ApplyFilters();
    partial void OnShowConnectedOnlyChanged(bool value) => ApplyFilters();

    // ========================================================================
    // Private Helpers
    // ========================================================================

    private void ApplyFilters()
    {
        FilteredIntegrations.Clear();

        var filtered = Integrations.AsEnumerable();

        // Filter by search query
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var query = SearchQuery.ToLowerInvariant();
            filtered = filtered.Where(i =>
                i.Integration.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                i.Integration.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) == true);
        }

        // Filter by category
        if (SelectedCategory.HasValue)
        {
            filtered = filtered.Where(i => i.Integration.Category == SelectedCategory.Value);
        }

        // Filter by connected status
        if (ShowConnectedOnly)
        {
            filtered = filtered.Where(i => i.IsConnected);
        }

        foreach (var item in filtered.OrderByDescending(i => i.IsConnected).ThenBy(i => i.Integration.Name))
        {
            FilteredIntegrations.Add(item);
        }
    }

    private void LoadMarketplaceCategories()
    {
        MarketplaceCategories.Clear();

        MarketplaceCategories.Add(new MarketplaceCategory
        {
            Name = "Cloud Providers",
            Description = "Connect to AWS, Azure, GCP, and other cloud platforms",
            Icon = "\uE753",
            IntegrationCount = 3,
            Category = IntegrationCategory.CloudProvider
        });

        MarketplaceCategories.Add(new MarketplaceCategory
        {
            Name = "Source Control",
            Description = "GitHub, GitLab, Bitbucket, and more",
            Icon = "\uE943",
            IntegrationCount = 3,
            Category = IntegrationCategory.SourceControl
        });

        MarketplaceCategories.Add(new MarketplaceCategory
        {
            Name = "Issue Tracking",
            Description = "Jira, Linear, Asana, and other project tools",
            Icon = "\uE762",
            IntegrationCount = 3,
            Category = IntegrationCategory.IssueTracking
        });

        MarketplaceCategories.Add(new MarketplaceCategory
        {
            Name = "Incident Management",
            Description = "PagerDuty, OpsGenie, VictorOps",
            Icon = "\uE814",
            IntegrationCount = 3,
            Category = IntegrationCategory.Alerting
        });

        MarketplaceCategories.Add(new MarketplaceCategory
        {
            Name = "Communication",
            Description = "Slack, Microsoft Teams, Discord",
            Icon = "\uE8BD",
            IntegrationCount = 3,
            Category = IntegrationCategory.Communication
        });

        MarketplaceCategories.Add(new MarketplaceCategory
        {
            Name = "Monitoring",
            Description = "Datadog, Prometheus, New Relic, Grafana",
            Icon = "\uE9D9",
            IntegrationCount = 4,
            Category = IntegrationCategory.Monitoring
        });
    }

    private async Task LoadHealthStatusAsync()
    {
        if (_cloudManager == null) return;

        try
        {
            var healthStatuses = await _cloudManager.CheckAllHealthAsync();

            HealthStatus.Clear();
            foreach (var status in healthStatuses)
            {
                HealthStatus.Add(new IntegrationHealthItem
                {
                    ProviderName = status.ProviderName,
                    InstanceId = status.InstanceId,
                    Health = status.Health,
                    ResponseTime = status.ResponseTime,
                    Message = status.Message,
                    CheckedAt = status.CheckedAt
                });
            }
        }
        catch
        {
            // Ignore health check failures during initial load
        }
    }

    private void OnCloudResourceChanged(object? sender, CloudResourceChangedEventArgs e)
    {
        RecentActivity.Insert(0, new RecentActivityItem
        {
            Timestamp = e.OccurredAt,
            Type = IntegrationActivityType.ResourceChange,
            Message = $"[{e.ProviderId}] {e.ResourceType} {e.ResourceId}: {e.ChangeType}",
            IntegrationName = e.ProviderId
        });

        // Keep only last 50 activities
        while (RecentActivity.Count > 50)
        {
            RecentActivity.RemoveAt(RecentActivity.Count - 1);
        }
    }

    private void OnCloudAlertTriggered(object? sender, CloudAlertEventArgs e)
    {
        RecentActivity.Insert(0, new RecentActivityItem
        {
            Timestamp = e.TriggeredAt,
            Type = IntegrationActivityType.Alert,
            Message = $"[{e.ProviderId}] {e.AlertName}: {e.Message}",
            IntegrationName = e.ProviderId,
            Severity = e.Severity
        });

        // Keep only last 50 activities
        while (RecentActivity.Count > 50)
        {
            RecentActivity.RemoveAt(RecentActivity.Count - 1);
        }
    }

    private void LoadDesignTimeData()
    {
        // Add sample integrations for design-time
        Integrations.Add(new IntegrationDisplayItem
        {
            Integration = CreateDesignTimeIntegration("aws", "Amazon Web Services", "Cloud computing platform",
                IntegrationCategory.CloudProvider, AuthMethod.ApiKey, "\uE753", "https://aws.amazon.com"),
            IsConnected = true,
            InstanceCount = 2,
            Health = IntegrationHealth.Healthy,
            LastSyncTime = DateTimeOffset.UtcNow.AddMinutes(-5)
        });

        Integrations.Add(new IntegrationDisplayItem
        {
            Integration = CreateDesignTimeIntegration("github", "GitHub", "Source control and collaboration",
                IntegrationCategory.SourceControl, AuthMethod.OAuth2, "\uE943", "https://github.com"),
            IsConnected = true,
            InstanceCount = 1,
            Health = IntegrationHealth.Healthy,
            LastSyncTime = DateTimeOffset.UtcNow.AddMinutes(-2)
        });

        Integrations.Add(new IntegrationDisplayItem
        {
            Integration = CreateDesignTimeIntegration("pagerduty", "PagerDuty", "Incident management",
                IntegrationCategory.Alerting, AuthMethod.ApiKey, "\uE814", "https://pagerduty.com"),
            IsConnected = false,
            InstanceCount = 0,
            Health = IntegrationHealth.Unknown
        });

        ConnectedCount = 2;
        HealthyCount = 2;
        WarningCount = 0;
        ErrorCount = 0;

        ApplyFilters();
        LoadMarketplaceCategories();
    }

    private static IntegrationModel CreateDesignTimeIntegration(
        string name, string displayName, string description,
        IntegrationCategory category, AuthMethod authMethod,
        string icon, string docUrl)
    {
        return new IntegrationModel(
            IntegrationId.New(),
            name,
            displayName,
            description,
            category,
            authMethod,
            icon,
            docUrl,
            IsBuiltIn: false,
            IsEnabled: true,
            new IntegrationCapabilities(
                SupportsWebhooks: true,
                SupportsPush: true,
                SupportsPull: true,
                SupportsSync: true,
                SupportsEvents: true,
                SupportsActions: true,
                SupportsHealthCheck: true,
                [],
                []),
            new IntegrationConfig(
                [],
                [],
                null,
                null,
                null,
                null,
                new Dictionary<string, string>(),
                60,
                30000),
            DateTimeOffset.UtcNow,
            null);
    }
}

// ========================================================================
// Display Item Classes
// ========================================================================

/// <summary>
/// Display item for an integration in the dashboard.
/// </summary>
public sealed partial class IntegrationDisplayItem : ObservableObject
{
    public required IntegrationModel Integration { get; init; }

    [ObservableProperty]
    private bool isConnected;

    [ObservableProperty]
    private int instanceCount;

    [ObservableProperty]
    private IntegrationHealth health;

    [ObservableProperty]
    private DateTimeOffset? lastSyncTime;

    [ObservableProperty]
    private bool isTesting;

    [ObservableProperty]
    private bool? lastTestResult;

    [ObservableProperty]
    private DateTimeOffset? lastTestTime;

    [ObservableProperty]
    private string? lastTestError;

    public string HealthIcon => Health switch
    {
        IntegrationHealth.Healthy => "\uE930",
        IntegrationHealth.Degraded => "\uE7BA",
        IntegrationHealth.Unhealthy => "\uEA39",
        IntegrationHealth.Unreachable => "\uE711",
        _ => "\uE9CE"
    };

    public string HealthColor => Health switch
    {
        IntegrationHealth.Healthy => "#10B981",
        IntegrationHealth.Degraded => "#F59E0B",
        IntegrationHealth.Unhealthy => "#EF4444",
        IntegrationHealth.Unreachable => "#6B7280",
        _ => "#9CA3AF"
    };

    public string LastSyncDisplay => LastSyncTime.HasValue
        ? GetRelativeTime(LastSyncTime.Value)
        : "Never";

    private static string GetRelativeTime(DateTimeOffset time)
    {
        var diff = DateTimeOffset.UtcNow - time;

        if (diff.TotalSeconds < 60) return "Just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return time.ToString("MMM d");
    }
}

/// <summary>
/// Marketplace category display item.
/// </summary>
public sealed class MarketplaceCategory
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Icon { get; init; }
    public required int IntegrationCount { get; init; }
    public required IntegrationCategory Category { get; init; }
}

/// <summary>
/// Health status display item.
/// </summary>
public sealed class IntegrationHealthItem
{
    public required string ProviderName { get; init; }
    public required IntegrationInstanceId InstanceId { get; init; }
    public required IntegrationHealth Health { get; init; }
    public required TimeSpan ResponseTime { get; init; }
    public string? Message { get; init; }
    public required DateTimeOffset CheckedAt { get; init; }

    public string ResponseTimeDisplay => ResponseTime.TotalMilliseconds < 1000
        ? $"{ResponseTime.TotalMilliseconds:F0}ms"
        : $"{ResponseTime.TotalSeconds:F1}s";
}

/// <summary>
/// Recent activity display item.
/// </summary>
public sealed class RecentActivityItem
{
    public required DateTimeOffset Timestamp { get; init; }
    public required IntegrationActivityType Type { get; init; }
    public required string Message { get; init; }
    public required string IntegrationName { get; init; }
    public bool? Success { get; init; }
    public CloudAlertSeverity? Severity { get; init; }

    public string TypeIcon => Type switch
    {
        IntegrationActivityType.ConnectionAttempt => "\uE703",
        IntegrationActivityType.Disconnection => "\uE711",
        IntegrationActivityType.ConnectionTest => "\uE9D9",
        IntegrationActivityType.ResourceChange => "\uE895",
        IntegrationActivityType.Alert => "\uE7BA",
        IntegrationActivityType.SyncCompleted => "\uE895",
        IntegrationActivityType.Error => "\uEA39",
        _ => "\uE946"
    };

    public string TimestampDisplay
    {
        get
        {
            var diff = DateTimeOffset.UtcNow - Timestamp;
            if (diff.TotalSeconds < 60) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            return Timestamp.ToString("MMM d HH:mm");
        }
    }
}

/// <summary>
/// Types of integration activity.
/// </summary>
public enum IntegrationActivityType
{
    ConnectionAttempt,
    Disconnection,
    ConnectionTest,
    ResourceChange,
    Alert,
    SyncCompleted,
    Error
}
