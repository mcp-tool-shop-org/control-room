using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlRoom.Domain.Model;
using ControlRoom.Domain.Services;
using ControlRoom.Infrastructure.Storage.Queries;

namespace ControlRoom.App.ViewModels;

public partial class NewThingViewModel : ObservableObject
{
    private readonly ThingQueries _things;
    private readonly IAIAssistant? _aiAssistant;

    public NewThingViewModel(ThingQueries things, IAIAssistant? aiAssistant = null)
    {
        _things = things;
        _aiAssistant = aiAssistant;

        // Start with one default profile
        Profiles.Add(new ProfileEditorItem
        {
            Id = "default",
            Name = "Default",
            Args = "",
            Env = "",
            WorkingDir = ""
        });

        // Check AI availability
        _ = CheckAIAvailabilityAsync();
    }

    [ObservableProperty]
    private string name = "";

    [ObservableProperty]
    private string scriptPath = "";

    [ObservableProperty]
    private string workingDirectory = "";

    [ObservableProperty]
    private string errorMessage = "";

    [ObservableProperty]
    private bool canSave;

    [ObservableProperty]
    private bool showProfiles;

    [ObservableProperty]
    private bool isAIAvailable;

    [ObservableProperty]
    private bool showAIGenerator;

    [ObservableProperty]
    private string aiPrompt = "";

    [ObservableProperty]
    private string selectedLanguage = "PowerShell";

    [ObservableProperty]
    private bool isGenerating;

    [ObservableProperty]
    private string generatedScript = "";

    [ObservableProperty]
    private string generatedDocumentation = "";

    [ObservableProperty]
    private bool hasGeneratedScript;

    public ObservableCollection<ProfileEditorItem> Profiles { get; } = [];
    public ObservableCollection<string> GeneratedDependencies { get; } = [];

    public string[] AvailableLanguages { get; } = ["PowerShell", "Python", "Bash", "Batch", "Node.js"];

    partial void OnNameChanged(string value) => UpdateCanSave();
    partial void OnScriptPathChanged(string value) => UpdateCanSave();

    private void UpdateCanSave()
    {
        CanSave = !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(ScriptPath);
    }

    [RelayCommand]
    private void ToggleProfiles()
    {
        ShowProfiles = !ShowProfiles;
    }

    [RelayCommand]
    private void AddProfile()
    {
        var count = Profiles.Count + 1;
        Profiles.Add(new ProfileEditorItem
        {
            Id = $"profile-{count}",
            Name = $"Profile {count}",
            Args = "",
            Env = "",
            WorkingDir = ""
        });
        ShowProfiles = true;
    }

    [RelayCommand]
    private void RemoveProfile(ProfileEditorItem? profile)
    {
        if (profile is null || Profiles.Count <= 1) return;
        Profiles.Remove(profile);
    }

    [RelayCommand]
    private async Task BrowseScriptAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select a script file",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".ps1", ".py", ".cmd", ".bat", ".sh", ".exe" } }
                })
            });

            if (result is not null)
            {
                ScriptPath = result.FullPath;

                // Auto-fill name from filename if empty
                if (string.IsNullOrWhiteSpace(Name))
                {
                    Name = Path.GetFileNameWithoutExtension(result.FileName);
                }

                // Auto-fill working directory
                if (string.IsNullOrWhiteSpace(WorkingDirectory))
                {
                    WorkingDirectory = Path.GetDirectoryName(result.FullPath) ?? "";
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanSave) return;

        ErrorMessage = "";

        // Validate script exists
        if (!File.Exists(ScriptPath))
        {
            ErrorMessage = "Script file not found";
            return;
        }

        try
        {
            // Build profiles from editor items
            var profiles = Profiles.Select(p => new ThingProfile
            {
                Id = SanitizeId(p.Name),
                Name = p.Name.Trim(),
                Args = p.Args?.Trim() ?? "",
                Env = ParseEnvString(p.Env),
                WorkingDir = string.IsNullOrWhiteSpace(p.WorkingDir) ? null : p.WorkingDir.Trim()
            }).ToList();

            var config = new ThingConfig
            {
                Path = ScriptPath,
                WorkingDir = string.IsNullOrWhiteSpace(WorkingDirectory) ? null : WorkingDirectory,
                Profiles = profiles
            };

            var thing = new Thing(
                ThingId.New(),
                Name.Trim(),
                ThingKind.LocalScript,
                config.ToJson(),
                DateTimeOffset.UtcNow
            );

            await Task.Run(() => _things.InsertThing(thing));

            // Navigate back to Things page
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save: {ex.Message}";
        }
    }

    /// <summary>
    /// Convert a display name to a safe ID (lowercase, no spaces)
    /// </summary>
    private static string SanitizeId(string name)
    {
        return name.Trim().ToLowerInvariant().Replace(" ", "-");
    }

    /// <summary>
    /// Parse "KEY=value" lines into a dictionary
    /// </summary>
    private static Dictionary<string, string> ParseEnvString(string? envString)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(envString)) return result;

        var lines = envString.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            var eqIdx = trimmed.IndexOf('=');
            if (eqIdx > 0)
            {
                var key = trimmed[..eqIdx].Trim();
                var value = trimmed[(eqIdx + 1)..].Trim();
                if (!string.IsNullOrEmpty(key))
                {
                    result[key] = value;
                }
            }
        }
        return result;
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private void ToggleAIGenerator()
    {
        ShowAIGenerator = !ShowAIGenerator;
    }

    [RelayCommand]
    private async Task GenerateScriptAsync()
    {
        if (_aiAssistant is null || string.IsNullOrWhiteSpace(AiPrompt) || IsGenerating)
            return;

        IsGenerating = true;
        ErrorMessage = "";
        GeneratedDependencies.Clear();

        try
        {
            var language = SelectedLanguage switch
            {
                "PowerShell" => ScriptLanguage.PowerShell,
                "Python" => ScriptLanguage.Python,
                "Bash" => ScriptLanguage.Bash,
                "Batch" => ScriptLanguage.Batch,
                "Node.js" => ScriptLanguage.Node,
                _ => ScriptLanguage.PowerShell
            };

            var result = await _aiAssistant.GenerateScriptAsync(AiPrompt, language);

            GeneratedScript = result.Content;
            GeneratedDocumentation = result.Documentation ?? "";
            HasGeneratedScript = !string.IsNullOrWhiteSpace(result.Content);

            foreach (var dep in result.RequiredDependencies)
            {
                GeneratedDependencies.Add(dep);
            }

            // Auto-fill name from the prompt if empty
            if (string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(AiPrompt))
            {
                // Create a short name from the prompt
                var words = AiPrompt.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(4);
                Name = string.Join(" ", words);
                if (Name.Length > 30) Name = Name[..30];
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Generation failed: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task SaveGeneratedScriptAsync()
    {
        if (!HasGeneratedScript || string.IsNullOrWhiteSpace(GeneratedScript))
            return;

        try
        {
            // Determine file extension based on language
            var extension = SelectedLanguage switch
            {
                "PowerShell" => ".ps1",
                "Python" => ".py",
                "Bash" => ".sh",
                "Batch" => ".bat",
                "Node.js" => ".js",
                _ => ".ps1"
            };

            // Create filename from name or prompt
            var baseName = string.IsNullOrWhiteSpace(Name)
                ? "generated-script"
                : SanitizeFilename(Name);

            // Get scripts directory
            var scriptsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ControlRoom",
                "scripts");
            Directory.CreateDirectory(scriptsDir);

            var scriptPath = Path.Combine(scriptsDir, baseName + extension);

            // Handle duplicates
            var counter = 1;
            while (File.Exists(scriptPath))
            {
                scriptPath = Path.Combine(scriptsDir, $"{baseName}-{counter}{extension}");
                counter++;
            }

            // Write the script
            await File.WriteAllTextAsync(scriptPath, GeneratedScript);

            // Update the form fields
            ScriptPath = scriptPath;
            if (string.IsNullOrWhiteSpace(WorkingDirectory))
            {
                WorkingDirectory = scriptsDir;
            }

            // Hide AI generator panel
            ShowAIGenerator = false;

            ErrorMessage = "";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save script: {ex.Message}";
        }
    }

    private static string SanitizeFilename(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return sanitized.Replace(' ', '-').ToLowerInvariant();
    }

    private async Task CheckAIAvailabilityAsync()
    {
        if (_aiAssistant is null)
        {
            IsAIAvailable = false;
            return;
        }

        try
        {
            IsAIAvailable = await _aiAssistant.IsAvailableAsync();
        }
        catch
        {
            IsAIAvailable = false;
        }
    }
}

/// <summary>
/// Editable profile item for the UI
/// </summary>
public partial class ProfileEditorItem : ObservableObject
{
    [ObservableProperty]
    private string id = "";

    [ObservableProperty]
    private string name = "";

    [ObservableProperty]
    private string args = "";

    /// <summary>
    /// Environment variables as "KEY=value" lines (one per line)
    /// </summary>
    [ObservableProperty]
    private string env = "";

    [ObservableProperty]
    private string workingDir = "";
}
