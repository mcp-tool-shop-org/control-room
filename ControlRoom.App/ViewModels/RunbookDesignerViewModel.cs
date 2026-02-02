using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlRoom.Domain.Model;
using ControlRoom.Infrastructure.Storage.Queries;

namespace ControlRoom.App.ViewModels;

public partial class RunbookDesignerViewModel : ObservableObject, IQueryAttributable
{
    private readonly RunbookQueries _runbooks;
    private readonly ThingQueries _things;

    private RunbookId? _editingRunbookId;

    public RunbookDesignerViewModel(RunbookQueries runbooks, ThingQueries things)
    {
        _runbooks = runbooks;
        _things = things;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("runbookId", out var id) && id is string idStr)
        {
            _editingRunbookId = new RunbookId(Guid.Parse(idStr));
            LoadRunbook(_editingRunbookId.Value);
        }
    }

    private void LoadRunbook(RunbookId runbookId)
    {
        var runbook = _runbooks.GetRunbook(runbookId);
        if (runbook is null) return;

        Name = runbook.Name;
        Description = runbook.Description;
        IsEnabled = runbook.IsEnabled;
        PageTitle = "Edit Runbook";

        Steps.Clear();
        foreach (var step in runbook.Steps)
        {
            Steps.Add(new StepDesignerItem(step, _things.ListThings()));
        }

        // Load trigger
        if (runbook.Trigger is ScheduleTrigger schedule)
        {
            SelectedTriggerType = "Schedule";
            CronExpression = schedule.CronExpression;
        }
        else if (runbook.Trigger is WebhookTrigger webhook)
        {
            SelectedTriggerType = "Webhook";
            WebhookSecret = webhook.Secret;
        }
        else if (runbook.Trigger is FileWatchTrigger fileWatch)
        {
            SelectedTriggerType = "File Watch";
            WatchPath = fileWatch.Path;
            WatchPattern = fileWatch.Pattern;
        }
        else
        {
            SelectedTriggerType = "Manual";
        }

        UpdateValidation();
    }

    [ObservableProperty]
    private string pageTitle = "New Runbook";

    [ObservableProperty]
    private string name = "";

    [ObservableProperty]
    private string description = "";

    [ObservableProperty]
    private bool isEnabled = true;

    [ObservableProperty]
    private string errorMessage = "";

    [ObservableProperty]
    private bool canSave;

    [ObservableProperty]
    private bool hasValidationErrors;

    [ObservableProperty]
    private string validationErrorsText = "";

    // Trigger settings
    [ObservableProperty]
    private string selectedTriggerType = "Manual";

    [ObservableProperty]
    private string cronExpression = "";

    [ObservableProperty]
    private string webhookSecret = "";

    [ObservableProperty]
    private string watchPath = "";

    [ObservableProperty]
    private string watchPattern = "*.*";

    public string[] TriggerTypes { get; } = ["Manual", "Schedule", "Webhook", "File Watch"];

    public ObservableCollection<StepDesignerItem> Steps { get; } = [];
    public ObservableCollection<ThingListItem> AvailableThings { get; } = [];

    partial void OnNameChanged(string value) => UpdateValidation();
    partial void OnSelectedTriggerTypeChanged(string value) => OnPropertyChanged(nameof(ShowScheduleOptions));

    public bool ShowScheduleOptions => SelectedTriggerType == "Schedule";
    public bool ShowWebhookOptions => SelectedTriggerType == "Webhook";
    public bool ShowFileWatchOptions => SelectedTriggerType == "File Watch";

    [RelayCommand]
    private async Task LoadThingsAsync()
    {
        await Task.Run(() =>
        {
            var things = _things.ListThings();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                AvailableThings.Clear();
                foreach (var thing in things)
                {
                    AvailableThings.Add(thing);
                }
            });
        });
    }

    [RelayCommand]
    private void AddStep()
    {
        var stepNumber = Steps.Count + 1;
        var step = new StepDesignerItem
        {
            StepId = $"step-{stepNumber}",
            Name = $"Step {stepNumber}",
            SelectedCondition = "On Success"
        };
        step.AvailableThings = AvailableThings.ToList();
        step.AvailableDependencies = Steps.Select(s => s.StepId).ToList();
        Steps.Add(step);
        UpdateValidation();
    }

    [RelayCommand]
    private void RemoveStep(StepDesignerItem? step)
    {
        if (step is null) return;

        Steps.Remove(step);

        // Update dependencies to remove references to deleted step
        foreach (var s in Steps)
        {
            s.SelectedDependencies.Remove(step.StepId);
        }

        // Update available dependencies for remaining steps
        UpdateAvailableDependencies();
        UpdateValidation();
    }

    [RelayCommand]
    private void MoveStepUp(StepDesignerItem? step)
    {
        if (step is null) return;
        var index = Steps.IndexOf(step);
        if (index > 0)
        {
            Steps.Move(index, index - 1);
        }
    }

    [RelayCommand]
    private void MoveStepDown(StepDesignerItem? step)
    {
        if (step is null) return;
        var index = Steps.IndexOf(step);
        if (index < Steps.Count - 1)
        {
            Steps.Move(index, index + 1);
        }
    }

    private void UpdateAvailableDependencies()
    {
        var allStepIds = Steps.Select(s => s.StepId).ToList();
        foreach (var step in Steps)
        {
            step.AvailableDependencies = allStepIds.Where(id => id != step.StepId).ToList();
        }
    }

    private void UpdateValidation()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (Steps.Count == 0)
            errors.Add("At least one step is required");

        foreach (var step in Steps)
        {
            if (string.IsNullOrWhiteSpace(step.StepId))
                errors.Add($"Step ID is required");
            if (step.SelectedThing is null)
                errors.Add($"Step '{step.Name}' needs a script selected");
        }

        // Check for cycles
        if (Steps.Count > 0 && !errors.Any(e => e.Contains("script")))
        {
            var tempRunbook = BuildRunbook();
            if (tempRunbook.HasCycle())
                errors.Add("Steps contain a dependency cycle");
        }

        HasValidationErrors = errors.Count > 0;
        ValidationErrorsText = string.Join("\n", errors);
        CanSave = !HasValidationErrors;
    }

    private Runbook BuildRunbook()
    {
        var steps = Steps.Select(s => new RunbookStep(
            s.StepId,
            s.Name,
            s.SelectedThing?.ThingId ?? ThingId.New(),
            s.SelectedProfile,
            s.SelectedCondition switch
            {
                "Always" => StepCondition.Always,
                "On Failure" => StepCondition.OnFailure,
                "Expression" => StepCondition.FromExpression(s.ConditionExpression),
                _ => StepCondition.OnSuccess
            },
            s.SelectedDependencies.ToList(),
            s.EnableRetry ? new RetryPolicy(s.MaxRetries, TimeSpan.FromSeconds(s.RetryDelaySeconds), 2.0, TimeSpan.FromMinutes(5)) : null,
            s.TimeoutSeconds > 0 ? TimeSpan.FromSeconds(s.TimeoutSeconds) : null,
            string.IsNullOrWhiteSpace(s.ArgumentsOverride) ? null : s.ArgumentsOverride
        )).ToList();

        RunbookTrigger? trigger = SelectedTriggerType switch
        {
            "Schedule" => new ScheduleTrigger(CronExpression),
            "Webhook" => new WebhookTrigger(string.IsNullOrEmpty(WebhookSecret) ? Guid.NewGuid().ToString() : WebhookSecret),
            "File Watch" => new FileWatchTrigger(WatchPath, WatchPattern),
            _ => new ManualTrigger()
        };

        var now = DateTimeOffset.UtcNow;
        return new Runbook(
            _editingRunbookId ?? RunbookId.New(),
            Name.Trim(),
            Description.Trim(),
            steps,
            trigger,
            IsEnabled,
            _editingRunbookId.HasValue ? now : now, // Use current time for createdAt when editing
            now
        );
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        UpdateValidation();
        if (!CanSave) return;

        ErrorMessage = "";

        try
        {
            var runbook = BuildRunbook();
            var validation = runbook.Validate();

            if (!validation.IsValid)
            {
                ErrorMessage = string.Join("; ", validation.Errors);
                return;
            }

            await Task.Run(() =>
            {
                if (_editingRunbookId.HasValue)
                {
                    _runbooks.UpdateRunbook(runbook);
                }
                else
                {
                    _runbooks.InsertRunbook(runbook);
                }
            });

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private void GenerateWebhookSecret()
    {
        WebhookSecret = Guid.NewGuid().ToString("N");
    }
}

/// <summary>
/// Represents a step in the designer UI
/// </summary>
public partial class StepDesignerItem : ObservableObject
{
    public StepDesignerItem() { }

    public StepDesignerItem(RunbookStep step, IReadOnlyList<ThingListItem> things)
    {
        StepId = step.StepId;
        Name = step.Name;
        SelectedProfile = step.ProfileId;
        ArgumentsOverride = step.ArgumentsOverride ?? "";
        TimeoutSeconds = step.Timeout.HasValue ? (int)step.Timeout.Value.TotalSeconds : 0;

        AvailableThings = things.ToList();
        SelectedThing = things.FirstOrDefault(t => t.ThingId == step.ThingId);

        SelectedCondition = step.Condition.Type switch
        {
            ConditionType.Always => "Always",
            ConditionType.OnFailure => "On Failure",
            ConditionType.Expression => "Expression",
            _ => "On Success"
        };

        if (step.Condition.Type == ConditionType.Expression)
        {
            ConditionExpression = step.Condition.Expression ?? "";
        }

        foreach (var dep in step.DependsOn)
        {
            SelectedDependencies.Add(dep);
        }

        if (step.Retry is not null)
        {
            EnableRetry = true;
            MaxRetries = step.Retry.MaxAttempts;
            RetryDelaySeconds = (int)step.Retry.InitialDelay.TotalSeconds;
        }
    }

    [ObservableProperty]
    private string stepId = "";

    [ObservableProperty]
    private string name = "";

    [ObservableProperty]
    private ThingListItem? selectedThing;

    [ObservableProperty]
    private string selectedProfile = "default";

    [ObservableProperty]
    private string selectedCondition = "On Success";

    [ObservableProperty]
    private string conditionExpression = "";

    [ObservableProperty]
    private string argumentsOverride = "";

    [ObservableProperty]
    private int timeoutSeconds;

    [ObservableProperty]
    private bool enableRetry;

    [ObservableProperty]
    private int maxRetries = 3;

    [ObservableProperty]
    private int retryDelaySeconds = 5;

    [ObservableProperty]
    private bool isExpanded;

    public List<ThingListItem> AvailableThings { get; set; } = [];
    public List<string> AvailableDependencies { get; set; } = [];
    public ObservableCollection<string> SelectedDependencies { get; } = [];

    public string[] ConditionOptions { get; } = ["On Success", "On Failure", "Always", "Expression"];

    public bool ShowExpressionInput => SelectedCondition == "Expression";

    partial void OnSelectedConditionChanged(string value) => OnPropertyChanged(nameof(ShowExpressionInput));

    [RelayCommand]
    private void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }

    [RelayCommand]
    private void AddDependency(string? stepId)
    {
        if (!string.IsNullOrEmpty(stepId) && !SelectedDependencies.Contains(stepId))
        {
            SelectedDependencies.Add(stepId);
        }
    }

    [RelayCommand]
    private void RemoveDependency(string? stepId)
    {
        if (!string.IsNullOrEmpty(stepId))
        {
            SelectedDependencies.Remove(stepId);
        }
    }
}
