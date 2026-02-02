namespace ControlRoom.Domain.Services;

/// <summary>
/// AI assistant for script intelligence - error analysis, code generation, and suggestions.
/// Inspired by Warp AI and Fig terminal features.
/// </summary>
public interface IAIAssistant
{
    /// <summary>
    /// Explain an error in human-readable terms.
    /// </summary>
    Task<ErrorExplanation> ExplainErrorAsync(
        ErrorContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Suggest fixes for an error based on script content and error output.
    /// </summary>
    Task<FixSuggestions> SuggestFixAsync(
        ErrorContext context,
        string scriptContent,
        CancellationToken ct = default);

    /// <summary>
    /// Generate a script from natural language description.
    /// </summary>
    Task<GeneratedScript> GenerateScriptAsync(
        string description,
        ScriptLanguage language,
        CancellationToken ct = default);

    /// <summary>
    /// Autocomplete arguments based on script analysis and history.
    /// </summary>
    Task<ArgumentSuggestions> AutocompleteArgsAsync(
        string scriptPath,
        string partialArgs,
        IReadOnlyList<string> previousArgs,
        CancellationToken ct = default);

    /// <summary>
    /// Generate documentation for a script.
    /// </summary>
    Task<ScriptDocumentation> GenerateDocumentationAsync(
        string scriptContent,
        string scriptPath,
        CancellationToken ct = default);

    /// <summary>
    /// Check if the AI service is available.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}

/// <summary>
/// Context for error analysis requests.
/// </summary>
public sealed record ErrorContext(
    string ErrorOutput,
    int? ExitCode,
    string? ScriptPath,
    string? Arguments,
    IReadOnlyDictionary<string, string>? Environment,
    TimeSpan? Duration);

/// <summary>
/// AI-generated error explanation.
/// </summary>
public sealed record ErrorExplanation(
    string Summary,
    string RootCause,
    ErrorSeverity Severity,
    IReadOnlyList<string> RelatedConcepts,
    double Confidence);

/// <summary>
/// Error severity classification.
/// </summary>
public enum ErrorSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// AI-generated fix suggestions.
/// </summary>
public sealed record FixSuggestions(
    IReadOnlyList<FixSuggestion> Suggestions,
    string? QuickFix,
    double Confidence);

/// <summary>
/// Single fix suggestion with code and explanation.
/// </summary>
public sealed record FixSuggestion(
    string Title,
    string Description,
    string? CodeChange,
    FixType Type,
    double Confidence);

/// <summary>
/// Type of fix being suggested.
/// </summary>
public enum FixType
{
    CodeChange,
    ArgumentChange,
    EnvironmentChange,
    DependencyInstall,
    PermissionFix,
    ConfigurationChange
}

/// <summary>
/// Script language for generation.
/// </summary>
public enum ScriptLanguage
{
    PowerShell,
    Python,
    Bash,
    Batch,
    Node
}

/// <summary>
/// AI-generated script.
/// </summary>
public sealed record GeneratedScript(
    string Content,
    string SuggestedFilename,
    ScriptLanguage Language,
    IReadOnlyList<string> RequiredDependencies,
    string? Documentation);

/// <summary>
/// Argument autocomplete suggestions.
/// </summary>
public sealed record ArgumentSuggestions(
    IReadOnlyList<ArgumentSuggestion> Suggestions,
    string? CurrentArgDescription);

/// <summary>
/// Single argument suggestion.
/// </summary>
public sealed record ArgumentSuggestion(
    string Value,
    string Description,
    ArgumentSource Source,
    double Relevance);

/// <summary>
/// Source of the argument suggestion.
/// </summary>
public enum ArgumentSource
{
    ScriptAnalysis,
    History,
    AIGenerated,
    DefaultValue
}

/// <summary>
/// AI-generated script documentation.
/// </summary>
public sealed record ScriptDocumentation(
    string Title,
    string Description,
    IReadOnlyList<ParameterDoc> Parameters,
    IReadOnlyList<string> Examples,
    IReadOnlyList<string> Prerequisites,
    string? Notes);

/// <summary>
/// Documentation for a script parameter.
/// </summary>
public sealed record ParameterDoc(
    string Name,
    string Description,
    string? Type,
    bool Required,
    string? DefaultValue);
