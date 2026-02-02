using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlRoom.Application.UseCases;
using ControlRoom.Domain.Model;
using ControlRoom.Infrastructure.Storage.Queries;

namespace ControlRoom.App.ViewModels;

public partial class RunbooksViewModel : ObservableObject
{
    private readonly RunbookQueries _runbooks;
    private readonly IRunbookExecutor _executor;

    public RunbooksViewModel(RunbookQueries runbooks, IRunbookExecutor executor)
    {
        _runbooks = runbooks;
        _executor = executor;
    }

    public ObservableCollection<RunbookListItemViewModel> Runbooks { get; } = [];
    public ObservableCollection<RunbookExecutionListItem> RecentExecutions { get; } = [];

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private bool hasRunbooks;

    [ObservableProperty]
    private RunbookListItemViewModel? selectedRunbook;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await Task.Run(() =>
        {
            var items = _runbooks.ListRunbooks();
            var executions = _runbooks.ListExecutions(limit: 10);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Runbooks.Clear();
                foreach (var item in items)
                {
                    Runbooks.Add(new RunbookListItemViewModel(item));
                }
                HasRunbooks = Runbooks.Count > 0;

                RecentExecutions.Clear();
                foreach (var exec in executions)
                {
                    RecentExecutions.Add(exec);
                }
            });
        });
        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task NewRunbookAsync()
    {
        await Shell.Current.GoToAsync("runbook/new");
    }

    [RelayCommand]
    private async Task EditRunbookAsync(RunbookListItemViewModel? item)
    {
        if (item is null) return;
        await Shell.Current.GoToAsync($"runbook/edit?runbookId={item.RunbookId}");
    }

    [RelayCommand]
    private async Task ExecuteRunbookAsync(RunbookListItemViewModel? item)
    {
        if (item is null || !item.IsEnabled) return;

        var runbook = _runbooks.GetRunbook(item.RunbookId);
        if (runbook is null) return;

        var executionId = await _executor.ExecuteAsync(runbook, "Manual execution from UI");

        // Refresh to show the new execution
        await RefreshAsync();

        // Navigate to execution detail
        await Shell.Current.GoToAsync($"runbook/execution?executionId={executionId}");
    }

    [RelayCommand]
    private async Task ViewExecutionAsync(RunbookExecutionListItem? item)
    {
        if (item is null) return;
        await Shell.Current.GoToAsync($"runbook/execution?executionId={item.ExecutionId}");
    }

    [RelayCommand]
    private async Task DeleteRunbookAsync(RunbookListItemViewModel? item)
    {
        if (item is null) return;

        bool confirm = await Shell.Current.DisplayAlert(
            "Delete Runbook",
            $"Are you sure you want to delete '{item.Name}'? This will also delete all execution history.",
            "Delete",
            "Cancel");

        if (confirm)
        {
            await Task.Run(() => _runbooks.DeleteRunbook(item.RunbookId));
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task ToggleEnabledAsync(RunbookListItemViewModel? item)
    {
        if (item is null) return;

        var runbook = _runbooks.GetRunbook(item.RunbookId);
        if (runbook is null) return;

        var updated = runbook with { IsEnabled = !runbook.IsEnabled, UpdatedAt = DateTimeOffset.UtcNow };
        _runbooks.UpdateRunbook(updated);

        item.IsEnabled = updated.IsEnabled;
        await Task.CompletedTask;
    }
}

/// <summary>
/// View model wrapper for RunbookListItem
/// </summary>
public partial class RunbookListItemViewModel : ObservableObject
{
    private readonly RunbookListItem _item;

    public RunbookListItemViewModel(RunbookListItem item)
    {
        _item = item;
        isEnabled = item.IsEnabled;
    }

    public RunbookId RunbookId => _item.RunbookId;
    public string Name => _item.Name;
    public string Description => _item.Description;
    public int StepCount => _item.StepCount;
    public int Version => _item.Version;
    public DateTimeOffset CreatedAt => _item.CreatedAt;
    public DateTimeOffset UpdatedAt => _item.UpdatedAt;

    public string TriggerDisplay => _item.TriggerType switch
    {
        TriggerType.Manual => "Manual",
        TriggerType.Schedule => "Scheduled",
        TriggerType.Webhook => "Webhook",
        TriggerType.FileWatch => "File Watch",
        _ => "Manual"
    };

    public string StepCountText => _item.StepCount == 1 ? "1 step" : $"{_item.StepCount} steps";
    public string LastUpdatedText => GetRelativeTime(_item.UpdatedAt);

    [ObservableProperty]
    private bool isEnabled;

    public string StatusColor => IsEnabled ? "#2196F3" : "#9E9E9E";

    private static string GetRelativeTime(DateTimeOffset time)
    {
        var diff = DateTimeOffset.UtcNow - time;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return time.ToString("MMM d");
    }
}
