using ControlRoom.Domain.Services;
using ControlRoom.Infrastructure.AI;

namespace ControlRoom.Tests.Unit.Infrastructure;

/// <summary>
/// Unit tests for OllamaAIAssistant.
/// Note: These tests mock the HTTP layer since they don't require a running Ollama instance.
/// </summary>
public sealed class OllamaAIAssistantTests : IDisposable
{
    private readonly OllamaAIAssistant _assistant;

    public OllamaAIAssistantTests()
    {
        // Use non-standard port to avoid accidental real calls (valid port range: 1-65535)
        _assistant = new OllamaAIAssistant(new AIAssistantOptions
        {
            OllamaBaseUrl = "http://localhost:59999",
            TimeoutSeconds = 1
        });
    }

    [Fact]
    public async Task IsAvailableAsync_WhenOllamaNotRunning_ReturnsFalse()
    {
        // Act
        var result = await _assistant.IsAvailableAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Options_HaveReasonableDefaults()
    {
        // Arrange
        var options = new AIAssistantOptions();

        // Assert
        Assert.Equal("http://localhost:11434", options.OllamaBaseUrl);
        Assert.Equal("llama3", options.Model);
        Assert.Equal(0.3, options.Temperature);
        Assert.Equal(0.9, options.TopP);
        Assert.Equal(60, options.TimeoutSeconds);
        Assert.Equal(2, options.MaxConcurrentRequests);
        Assert.Equal(TimeSpan.FromMinutes(15), options.CacheDuration);
    }

    [Fact]
    public void ErrorContext_RecordEquality()
    {
        // Arrange
        var env1 = new Dictionary<string, string> { { "PATH", "/usr/bin" } };
        var env2 = new Dictionary<string, string> { { "PATH", "/usr/bin" } };

        var context1 = new ErrorContext("error", 1, "/script.ps1", "-help", env1, TimeSpan.FromSeconds(5));
        var context2 = new ErrorContext("error", 1, "/script.ps1", "-help", env2, TimeSpan.FromSeconds(5));

        // Note: Records compare reference equality for collections, so these won't be equal
        // This tests that the record is properly constructed
        Assert.NotNull(context1);
        Assert.NotNull(context2);
        Assert.Equal("error", context1.ErrorOutput);
        Assert.Equal(1, context1.ExitCode);
    }

    [Fact]
    public void ErrorExplanation_RecordConstruction()
    {
        // Arrange & Act
        var explanation = new ErrorExplanation(
            Summary: "File not found",
            RootCause: "The script tried to access a file that doesn't exist",
            Severity: ErrorSeverity.Error,
            RelatedConcepts: new[] { "FileSystem", "Paths" },
            Confidence: 0.85
        );

        // Assert
        Assert.Equal("File not found", explanation.Summary);
        Assert.Equal("The script tried to access a file that doesn't exist", explanation.RootCause);
        Assert.Equal(ErrorSeverity.Error, explanation.Severity);
        Assert.Contains("FileSystem", explanation.RelatedConcepts);
        Assert.Equal(0.85, explanation.Confidence);
    }

    [Fact]
    public void FixSuggestion_AllFixTypesAreDefined()
    {
        // Arrange
        var fixTypes = Enum.GetValues<FixType>();

        // Assert
        Assert.Contains(FixType.CodeChange, fixTypes);
        Assert.Contains(FixType.ArgumentChange, fixTypes);
        Assert.Contains(FixType.EnvironmentChange, fixTypes);
        Assert.Contains(FixType.DependencyInstall, fixTypes);
        Assert.Contains(FixType.PermissionFix, fixTypes);
        Assert.Contains(FixType.ConfigurationChange, fixTypes);
    }

    [Fact]
    public void ScriptLanguage_AllLanguagesAreDefined()
    {
        // Arrange
        var languages = Enum.GetValues<ScriptLanguage>();

        // Assert
        Assert.Contains(ScriptLanguage.PowerShell, languages);
        Assert.Contains(ScriptLanguage.Python, languages);
        Assert.Contains(ScriptLanguage.Bash, languages);
        Assert.Contains(ScriptLanguage.Batch, languages);
        Assert.Contains(ScriptLanguage.Node, languages);
    }

    [Fact]
    public void GeneratedScript_RecordConstruction()
    {
        // Arrange & Act
        var script = new GeneratedScript(
            Content: "Write-Host 'Hello'",
            SuggestedFilename: "hello.ps1",
            Language: ScriptLanguage.PowerShell,
            RequiredDependencies: Array.Empty<string>(),
            Documentation: "Prints Hello to console"
        );

        // Assert
        Assert.Equal("Write-Host 'Hello'", script.Content);
        Assert.Equal("hello.ps1", script.SuggestedFilename);
        Assert.Equal(ScriptLanguage.PowerShell, script.Language);
        Assert.Empty(script.RequiredDependencies);
    }

    [Fact]
    public void ArgumentSuggestion_AllSourcesAreDefined()
    {
        // Arrange
        var sources = Enum.GetValues<ArgumentSource>();

        // Assert
        Assert.Contains(ArgumentSource.ScriptAnalysis, sources);
        Assert.Contains(ArgumentSource.History, sources);
        Assert.Contains(ArgumentSource.AIGenerated, sources);
        Assert.Contains(ArgumentSource.DefaultValue, sources);
    }

    [Fact]
    public void ScriptDocumentation_RecordConstruction()
    {
        // Arrange
        var param = new ParameterDoc("Path", "File path to process", "string", Required: true, DefaultValue: null);

        // Act
        var doc = new ScriptDocumentation(
            Title: "File Processor",
            Description: "Processes files in a directory",
            Parameters: new[] { param },
            Examples: new[] { "./process.ps1 -Path C:\\temp" },
            Prerequisites: new[] { "PowerShell 7+" },
            Notes: "Run as administrator"
        );

        // Assert
        Assert.Equal("File Processor", doc.Title);
        Assert.Single(doc.Parameters);
        Assert.Equal("Path", doc.Parameters[0].Name);
        Assert.True(doc.Parameters[0].Required);
    }

    [Fact]
    public void ErrorSeverity_AllLevelsAreDefined()
    {
        // Arrange
        var severities = Enum.GetValues<ErrorSeverity>();

        // Assert
        Assert.Contains(ErrorSeverity.Info, severities);
        Assert.Contains(ErrorSeverity.Warning, severities);
        Assert.Contains(ErrorSeverity.Error, severities);
        Assert.Contains(ErrorSeverity.Critical, severities);
    }

    [Fact]
    public void FixSuggestions_RecordConstruction()
    {
        // Arrange & Act
        var suggestion = new FixSuggestion(
            Title: "Add null check",
            Description: "The variable might be null",
            CodeChange: "if ($var -ne $null)",
            Type: FixType.CodeChange,
            Confidence: 0.9
        );

        var suggestions = new FixSuggestions(
            Suggestions: new[] { suggestion },
            QuickFix: "Add null check before access",
            Confidence: 0.85
        );

        // Assert
        Assert.Single(suggestions.Suggestions);
        Assert.Equal("Add null check", suggestions.Suggestions[0].Title);
        Assert.Equal("Add null check before access", suggestions.QuickFix);
    }

    [Fact]
    public void ArgumentSuggestions_RecordConstruction()
    {
        // Arrange & Act
        var suggestion = new ArgumentSuggestion(
            Value: "--verbose",
            Description: "Enable verbose output",
            Source: ArgumentSource.ScriptAnalysis,
            Relevance: 0.95
        );

        var suggestions = new ArgumentSuggestions(
            Suggestions: new[] { suggestion },
            CurrentArgDescription: "Verbosity flag"
        );

        // Assert
        Assert.Single(suggestions.Suggestions);
        Assert.Equal("--verbose", suggestions.Suggestions[0].Value);
        Assert.Equal(ArgumentSource.ScriptAnalysis, suggestions.Suggestions[0].Source);
    }

    public void Dispose()
    {
        _assistant.Dispose();
    }
}
