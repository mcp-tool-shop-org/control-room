using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlRoom.Domain.Model;
using ControlRoom.Infrastructure.Storage.Queries;

namespace ControlRoom.App.ViewModels;

/// <summary>
/// ViewModel for the observability dashboard
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly MetricsQueries _metrics;
    private readonly RunQueries _runs;
    private readonly RunbookQueries _runbooks;
    private System.Timers.Timer? _refreshTimer;

    public DashboardViewModel(MetricsQueries metrics, RunQueries runs, RunbookQueries runbooks)
    {
        _metrics = metrics;
        _runs = runs;
        _runbooks = runbooks;
    }

    #region Observable Properties

    // Summary cards
    [ObservableProperty]
    private int totalScriptsToday;

    [ObservableProperty]
    private int successfulScriptsToday;

    [ObservableProperty]
    private int failedScriptsToday;

    [ObservableProperty]
    private double successRate;

    [ObservableProperty]
    private int activeAlerts;

    [ObservableProperty]
    private int healthyChecks;

    [ObservableProperty]
    private int unhealthyChecks;

    [ObservableProperty]
    private double avgDurationMs;

    // Charts data
    public ObservableCollection<TimeSeriesDataPoint> ExecutionTrend { get; } = [];
    public ObservableCollection<TimeSeriesDataPoint> DurationTrend { get; } = [];
    public ObservableCollection<StatusBreakdownItem> StatusBreakdown { get; } = [];

    // Recent activity
    public ObservableCollection<RecentExecutionItem> RecentExecutions { get; } = [];
    public ObservableCollection<AlertViewModel> ActiveAlertsList { get; } = [];

    // State
    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private bool isAutoRefreshEnabled = true;

    [ObservableProperty]
    private string lastUpdated = "Never";

    [ObservableProperty]
    private DashboardTimeRange selectedTimeRange = DashboardTimeRange.Last24Hours;

    #endregion

    #region Commands

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RefreshAsync();
        StartAutoRefresh();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;

        try
        {
            await Task.Run(() =>
            {
                var now = DateTimeOffset.UtcNow;
                var (from, to) = GetTimeRange(SelectedTimeRange);

                // Load summary statistics
                LoadSummaryStats(from, to);

                // Load execution trend
                LoadExecutionTrend(from, to);

                // Load duration trend
                LoadDurationTrend(from, to);

                // Load status breakdown
                LoadStatusBreakdown(from, to);

                // Load recent executions
                LoadRecentExecutions();

                // Load active alerts
                LoadActiveAlerts();
            });

            LastUpdated = DateTimeOffset.Now.ToString("HH:mm:ss");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private void ToggleAutoRefresh()
    {
        IsAutoRefreshEnabled = !IsAutoRefreshEnabled;
        if (IsAutoRefreshEnabled)
            StartAutoRefresh();
        else
            StopAutoRefresh();
    }

    [RelayCommand]
    private async Task SetTimeRangeAsync(DashboardTimeRange range)
    {
        SelectedTimeRange = range;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ViewAlertDetailsAsync(AlertViewModel? alert)
    {
        if (alert is null) return;
        // Navigate to alert details (could be a popup or separate page)
        await Shell.Current.DisplayAlert(
            $"Alert: {alert.RuleName}",
            $"Severity: {alert.Severity}\nMessage: {alert.Message}\nValue: {alert.CurrentValue}\nThreshold: {alert.Threshold}\nFired: {alert.FiredAt:g}",
            "OK");
    }

    [RelayCommand]
    private async Task AcknowledgeAlertAsync(AlertViewModel? alert)
    {
        if (alert is null) return;
        _metrics.AcknowledgeAlert(alert.AlertId);
        await RefreshAsync();
    }

    #endregion

    #region Private Methods

    private void LoadSummaryStats(DateTimeOffset from, DateTimeOffset to)
    {
        // Get runs from database for the time range
        var runs = _runs.ListRuns(100).Where(r => r.StartedAt >= from && r.StartedAt <= to).ToList();

        var total = runs.Count;
        var succeeded = runs.Count(r => r.Status == RunStatus.Succeeded);
        var failed = runs.Count(r => r.Status == RunStatus.Failed);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            TotalScriptsToday = total;
            SuccessfulScriptsToday = succeeded;
            FailedScriptsToday = failed;
            SuccessRate = total > 0 ? Math.Round(100.0 * succeeded / total, 1) : 0;
        });

        // Get average duration
        var durations = _metrics.QueryMetrics(MetricNames.ScriptDuration, from, to, limit: 1000);
        var avgDuration = durations.Count > 0 ? durations.Average(m => m.Value) : 0;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            AvgDurationMs = Math.Round(avgDuration, 0);
        });

        // Get alert counts
        var alerts = _metrics.GetActiveAlerts();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ActiveAlerts = alerts.Count;
        });

        // Get health check status
        var healthResults = _metrics.GetLatestHealthCheckResults();
        var healthy = healthResults.Count(r => r.Status == HealthStatus.Healthy);
        var unhealthy = healthResults.Count(r => r.Status != HealthStatus.Healthy);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            HealthyChecks = healthy;
            UnhealthyChecks = unhealthy;
        });
    }

    private void LoadExecutionTrend(DateTimeOffset from, DateTimeOffset to)
    {
        // Get execution count over time
        var runs = _runs.ListRuns(1000).Where(r => r.StartedAt >= from && r.StartedAt <= to).ToList();

        var bucketSize = GetBucketSize(SelectedTimeRange);
        var buckets = runs
            .GroupBy(r => new DateTimeOffset(
                r.StartedAt.Ticks / bucketSize.Ticks * bucketSize.Ticks,
                r.StartedAt.Offset))
            .Select(g => new TimeSeriesDataPoint(g.Key, g.Count(), "Executions"))
            .OrderBy(p => p.Timestamp)
            .ToList();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            ExecutionTrend.Clear();
            foreach (var bucket in buckets)
            {
                ExecutionTrend.Add(bucket);
            }
        });
    }

    private void LoadDurationTrend(DateTimeOffset from, DateTimeOffset to)
    {
        var bucketSize = GetBucketSize(SelectedTimeRange);
        var series = _metrics.GetTimeSeries(MetricNames.ScriptDuration, from, to, bucketSize);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            DurationTrend.Clear();
            foreach (var point in series.Points)
            {
                DurationTrend.Add(new TimeSeriesDataPoint(point.Timestamp, point.Value, "Duration (ms)"));
            }
        });
    }

    private void LoadStatusBreakdown(DateTimeOffset from, DateTimeOffset to)
    {
        var runs = _runs.ListRuns(1000).Where(r => r.StartedAt >= from && r.StartedAt <= to).ToList();

        var breakdown = runs
            .GroupBy(r => r.Status)
            .Select(g => new StatusBreakdownItem(
                g.Key.ToString(),
                g.Count(),
                GetStatusColor(g.Key)))
            .ToList();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusBreakdown.Clear();
            foreach (var item in breakdown)
            {
                StatusBreakdown.Add(item);
            }
        });
    }

    private void LoadRecentExecutions()
    {
        var runs = _runs.ListRuns(10);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            RecentExecutions.Clear();
            foreach (var run in runs)
            {
                // Get duration from summary if available
                var summary = run.GetParsedSummary();
                var duration = summary?.Duration;

                RecentExecutions.Add(new RecentExecutionItem(
                    run.RunId,
                    run.ThingName ?? "Unknown",
                    run.Status,
                    run.StartedAt,
                    duration
                ));
            }
        });
    }

    private void LoadActiveAlerts()
    {
        var alerts = _metrics.GetActiveAlerts();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            ActiveAlertsList.Clear();
            foreach (var alert in alerts)
            {
                ActiveAlertsList.Add(new AlertViewModel(
                    alert.Id,
                    alert.RuleName,
                    alert.Severity,
                    alert.Message,
                    alert.CurrentValue,
                    alert.Threshold,
                    alert.FiredAt
                ));
            }
        });
    }

    private (DateTimeOffset from, DateTimeOffset to) GetTimeRange(DashboardTimeRange range)
    {
        var now = DateTimeOffset.UtcNow;
        return range switch
        {
            DashboardTimeRange.LastHour => (now.AddHours(-1), now),
            DashboardTimeRange.Last6Hours => (now.AddHours(-6), now),
            DashboardTimeRange.Last24Hours => (now.AddHours(-24), now),
            DashboardTimeRange.Last7Days => (now.AddDays(-7), now),
            DashboardTimeRange.Last30Days => (now.AddDays(-30), now),
            _ => (now.AddHours(-24), now)
        };
    }

    private TimeSpan GetBucketSize(DashboardTimeRange range)
    {
        return range switch
        {
            DashboardTimeRange.LastHour => TimeSpan.FromMinutes(5),
            DashboardTimeRange.Last6Hours => TimeSpan.FromMinutes(15),
            DashboardTimeRange.Last24Hours => TimeSpan.FromHours(1),
            DashboardTimeRange.Last7Days => TimeSpan.FromHours(6),
            DashboardTimeRange.Last30Days => TimeSpan.FromDays(1),
            _ => TimeSpan.FromHours(1)
        };
    }

    private string GetStatusColor(RunStatus status)
    {
        return status switch
        {
            RunStatus.Succeeded => "#4CAF50",  // Green
            RunStatus.Failed => "#F44336",      // Red
            RunStatus.Running => "#2196F3",     // Blue
            RunStatus.Canceled => "#9E9E9E",   // Gray
            _ => "#757575"
        };
    }

    private void StartAutoRefresh()
    {
        _refreshTimer?.Dispose();
        _refreshTimer = new System.Timers.Timer(30000); // 30 seconds
        _refreshTimer.Elapsed += async (s, e) =>
        {
            if (IsAutoRefreshEnabled)
            {
                await RefreshAsync();
            }
        };
        _refreshTimer.Start();
    }

    private void StopAutoRefresh()
    {
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
        _refreshTimer = null;
    }

    public void Cleanup()
    {
        StopAutoRefresh();
    }

    #endregion
}

/// <summary>
/// Time range options for the dashboard
/// </summary>
public enum DashboardTimeRange
{
    LastHour,
    Last6Hours,
    Last24Hours,
    Last7Days,
    Last30Days
}

/// <summary>
/// Data point for time series charts
/// </summary>
public sealed record TimeSeriesDataPoint(
    DateTimeOffset Timestamp,
    double Value,
    string Label
)
{
    public string TimeLabel => Timestamp.ToString("HH:mm");
    public string DateLabel => Timestamp.ToString("MMM d");
}

/// <summary>
/// Item for status breakdown pie/bar chart
/// </summary>
public sealed record StatusBreakdownItem(
    string Status,
    int Count,
    string Color
)
{
    public string Label => $"{Status}: {Count}";
}

/// <summary>
/// Recent execution list item
/// </summary>
public sealed record RecentExecutionItem(
    RunId RunId,
    string ScriptName,
    RunStatus Status,
    DateTimeOffset StartedAt,
    TimeSpan? Duration
)
{
    public string DurationText => Duration.HasValue
        ? Duration.Value.TotalSeconds < 60
            ? $"{Duration.Value.TotalSeconds:F1}s"
            : $"{Duration.Value.TotalMinutes:F1}m"
        : "Running...";

    public string TimeAgo
    {
        get
        {
            var diff = DateTimeOffset.UtcNow - StartedAt;
            if (diff.TotalMinutes < 1) return "just now";
            if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
            return $"{(int)diff.TotalDays}d ago";
        }
    }

    public string StatusColor => Status switch
    {
        RunStatus.Succeeded => "#4CAF50",
        RunStatus.Failed => "#F44336",
        RunStatus.Running => "#2196F3",
        _ => "#9E9E9E"
    };
}

/// <summary>
/// Alert view model for display
/// </summary>
public sealed record AlertViewModel(
    AlertId AlertId,
    string RuleName,
    AlertSeverity Severity,
    string Message,
    double CurrentValue,
    double Threshold,
    DateTimeOffset FiredAt
)
{
    public string SeverityColor => Severity switch
    {
        AlertSeverity.Critical => "#D32F2F",
        AlertSeverity.Error => "#F44336",
        AlertSeverity.Warning => "#FF9800",
        AlertSeverity.Info => "#2196F3",
        _ => "#9E9E9E"
    };

    public string SeverityIcon => Severity switch
    {
        AlertSeverity.Critical => "⚠️",
        AlertSeverity.Error => "❌",
        AlertSeverity.Warning => "⚡",
        AlertSeverity.Info => "ℹ️",
        _ => "?"
    };

    public string TimeAgo
    {
        get
        {
            var diff = DateTimeOffset.UtcNow - FiredAt;
            if (diff.TotalMinutes < 1) return "just now";
            if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
            return $"{(int)diff.TotalDays}d ago";
        }
    }
}
