using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ControlRoom.Domain.Services;

namespace ControlRoom.Infrastructure.AI;

/// <summary>
/// AI assistant implementation using Ollama for local LLM inference.
/// Falls back to configured API endpoint if Ollama is unavailable.
/// </summary>
public sealed class OllamaAIAssistant : IAIAssistant, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly AIAssistantOptions _options;
    private readonly SemaphoreSlim _rateLimiter;
    private readonly Dictionary<string, CachedResponse> _cache = new();
    private readonly object _cacheLock = new();

    public OllamaAIAssistant(AIAssistantOptions? options = null)
    {
        _options = options ?? new AIAssistantOptions();
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_options.OllamaBaseUrl),
            Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds)
        };
        _rateLimiter = new SemaphoreSlim(_options.MaxConcurrentRequests);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ErrorExplanation> ExplainErrorAsync(
        ErrorContext context,
        CancellationToken ct = default)
    {
        var cacheKey = $"explain:{ComputeHash(context.ErrorOutput)}";
        if (TryGetCached<ErrorExplanation>(cacheKey, out var cached))
            return cached!;

        var prompt = BuildErrorExplanationPrompt(context);
        var response = await GenerateAsync(prompt, ct);

        var explanation = ParseErrorExplanation(response);
        CacheResponse(cacheKey, explanation);
        return explanation;
    }

    public async Task<FixSuggestions> SuggestFixAsync(
        ErrorContext context,
        string scriptContent,
        CancellationToken ct = default)
    {
        var prompt = BuildFixSuggestionPrompt(context, scriptContent);
        var response = await GenerateAsync(prompt, ct);
        return ParseFixSuggestions(response);
    }

    public async Task<GeneratedScript> GenerateScriptAsync(
        string description,
        ScriptLanguage language,
        CancellationToken ct = default)
    {
        var prompt = BuildScriptGenerationPrompt(description, language);
        var response = await GenerateAsync(prompt, ct);
        return ParseGeneratedScript(response, language);
    }

    public async Task<ArgumentSuggestions> AutocompleteArgsAsync(
        string scriptPath,
        string partialArgs,
        IReadOnlyList<string> previousArgs,
        CancellationToken ct = default)
    {
        var scriptContent = File.Exists(scriptPath)
            ? await File.ReadAllTextAsync(scriptPath, ct)
            : "";

        var prompt = BuildArgAutocompletePrompt(scriptContent, partialArgs, previousArgs);
        var response = await GenerateAsync(prompt, ct);
        return ParseArgumentSuggestions(response, previousArgs);
    }

    public async Task<ScriptDocumentation> GenerateDocumentationAsync(
        string scriptContent,
        string scriptPath,
        CancellationToken ct = default)
    {
        var prompt = BuildDocumentationPrompt(scriptContent, scriptPath);
        var response = await GenerateAsync(prompt, ct);
        return ParseDocumentation(response, scriptPath);
    }

    private async Task<string> GenerateAsync(string prompt, CancellationToken ct)
    {
        await _rateLimiter.WaitAsync(ct);
        try
        {
            var request = new OllamaGenerateRequest
            {
                Model = _options.Model,
                Prompt = prompt,
                Stream = false,
                Options = new OllamaOptions
                {
                    Temperature = _options.Temperature,
                    TopP = _options.TopP
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/generate", request, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(ct);
            return result?.Response ?? "";
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    #region Prompt Builders

    private static string BuildErrorExplanationPrompt(ErrorContext context)
    {
        var exitCode = context.ExitCode?.ToString() ?? "N/A";
        var script = context.ScriptPath ?? "Unknown";
        var args = context.Arguments ?? "None";
        var duration = context.Duration?.TotalSeconds.ToString("F1") ?? "N/A";
        var errorOutput = TruncateOutput(context.ErrorOutput, 2000);

        return string.Join("\n",
            "Analyze this script error and provide a JSON response:",
            "",
            $"Exit Code: {exitCode}",
            $"Script: {script}",
            $"Arguments: {args}",
            $"Duration: {duration}s",
            "",
            "Error Output:",
            "```",
            errorOutput,
            "```",
            "",
            "Respond with JSON only:",
            "{\"summary\": \"one-line summary\", \"rootCause\": \"explanation\", \"severity\": \"error\", \"relatedConcepts\": [], \"confidence\": 0.8}");
    }

    private static string BuildFixSuggestionPrompt(ErrorContext context, string scriptContent)
    {
        var errorOutput = TruncateOutput(context.ErrorOutput, 1000);
        var script = TruncateOutput(scriptContent, 2000);

        return string.Join("\n",
            "Suggest fixes for this script error:",
            "",
            "Error Output:",
            "```",
            errorOutput,
            "```",
            "",
            "Script Content:",
            "```",
            script,
            "```",
            "",
            "Respond with JSON only:",
            "{\"suggestions\": [{\"title\": \"Fix\", \"description\": \"desc\", \"codeChange\": null, \"type\": \"CodeChange\", \"confidence\": 0.8}], \"quickFix\": null, \"confidence\": 0.8}");
    }

    private static string BuildScriptGenerationPrompt(string description, ScriptLanguage language)
    {
        var langName = language switch
        {
            ScriptLanguage.PowerShell => "PowerShell",
            ScriptLanguage.Python => "Python",
            ScriptLanguage.Bash => "Bash",
            ScriptLanguage.Batch => "Windows Batch",
            ScriptLanguage.Node => "Node.js",
            _ => "PowerShell"
        };

        var extension = language switch
        {
            ScriptLanguage.PowerShell => ".ps1",
            ScriptLanguage.Python => ".py",
            ScriptLanguage.Bash => ".sh",
            ScriptLanguage.Batch => ".bat",
            ScriptLanguage.Node => ".js",
            _ => ".ps1"
        };

        return string.Join("\n",
            $"Generate a {langName} script for this task:",
            "",
            $"Task: {description}",
            "",
            "Requirements:",
            "- Include error handling",
            "- Add helpful comments",
            $"- Follow best practices for {langName}",
            "- Make it production-ready",
            "",
            "Respond with JSON only:",
            $"{{\"content\": \"script\", \"suggestedFilename\": \"script{extension}\", \"requiredDependencies\": [], \"documentation\": \"usage\"}}");
    }

    private static string BuildArgAutocompletePrompt(
        string scriptContent,
        string partialArgs,
        IReadOnlyList<string> previousArgs)
    {
        var historyStr = previousArgs.Any()
            ? string.Join("\n", previousArgs.Take(5))
            : "No history";

        var script = TruncateOutput(scriptContent, 1500);

        return string.Join("\n",
            "Suggest argument completions for this script:",
            "",
            "Script:",
            "```",
            script,
            "```",
            "",
            $"Current partial input: {partialArgs}",
            "",
            "Previous arguments used:",
            historyStr,
            "",
            "Respond with JSON only:",
            "{\"suggestions\": [{\"value\": \"--help\", \"description\": \"help\", \"source\": \"ScriptAnalysis\", \"relevance\": 0.9}], \"currentArgDescription\": null}");
    }

    private static string BuildDocumentationPrompt(string scriptContent, string scriptPath)
    {
        var filename = Path.GetFileName(scriptPath);
        var script = TruncateOutput(scriptContent, 3000);

        return string.Join("\n",
            "Generate documentation for this script:",
            "",
            $"Filename: {filename}",
            "",
            "Content:",
            "```",
            script,
            "```",
            "",
            "Respond with JSON only:",
            "{\"title\": \"Script\", \"description\": \"desc\", \"parameters\": [], \"examples\": [], \"prerequisites\": [], \"notes\": null}");
    }

    #endregion

    #region Response Parsers

    private static ErrorExplanation ParseErrorExplanation(string response)
    {
        try
        {
            var json = ExtractJson(response);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new ErrorExplanation(
                Summary: root.GetProperty("summary").GetString() ?? "Unknown error",
                RootCause: root.GetProperty("rootCause").GetString() ?? "Unable to determine root cause",
                Severity: ParseSeverity(root.GetProperty("severity").GetString()),
                RelatedConcepts: ParseStringArray(root, "relatedConcepts"),
                Confidence: root.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0.5
            );
        }
        catch
        {
            return new ErrorExplanation(
                "Unable to analyze error",
                response,
                ErrorSeverity.Error,
                Array.Empty<string>(),
                0.0
            );
        }
    }

    private static FixSuggestions ParseFixSuggestions(string response)
    {
        try
        {
            var json = ExtractJson(response);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var suggestions = new List<FixSuggestion>();
            if (root.TryGetProperty("suggestions", out var suggestionsArr))
            {
                foreach (var item in suggestionsArr.EnumerateArray())
                {
                    suggestions.Add(new FixSuggestion(
                        Title: item.GetProperty("title").GetString() ?? "Fix",
                        Description: item.GetProperty("description").GetString() ?? "",
                        CodeChange: item.TryGetProperty("codeChange", out var cc) ? cc.GetString() : null,
                        Type: ParseFixType(item.TryGetProperty("type", out var t) ? t.GetString() : null),
                        Confidence: item.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0.5
                    ));
                }
            }

            return new FixSuggestions(
                Suggestions: suggestions,
                QuickFix: root.TryGetProperty("quickFix", out var qf) ? qf.GetString() : null,
                Confidence: root.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0.5
            );
        }
        catch
        {
            return new FixSuggestions(Array.Empty<FixSuggestion>(), null, 0.0);
        }
    }

    private static GeneratedScript ParseGeneratedScript(string response, ScriptLanguage language)
    {
        try
        {
            var json = ExtractJson(response);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new GeneratedScript(
                Content: root.GetProperty("content").GetString() ?? "",
                SuggestedFilename: root.TryGetProperty("suggestedFilename", out var fn)
                    ? fn.GetString() ?? "script.ps1"
                    : "script.ps1",
                Language: language,
                RequiredDependencies: ParseStringArray(root, "requiredDependencies"),
                Documentation: root.TryGetProperty("documentation", out var d) ? d.GetString() : null
            );
        }
        catch
        {
            return new GeneratedScript(response, "script.ps1", language, Array.Empty<string>(), null);
        }
    }

    private static ArgumentSuggestions ParseArgumentSuggestions(
        string response,
        IReadOnlyList<string> previousArgs)
    {
        try
        {
            var json = ExtractJson(response);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var suggestions = new List<ArgumentSuggestion>();
            if (root.TryGetProperty("suggestions", out var suggestionsArr))
            {
                foreach (var item in suggestionsArr.EnumerateArray())
                {
                    suggestions.Add(new ArgumentSuggestion(
                        Value: item.GetProperty("value").GetString() ?? "",
                        Description: item.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                        Source: ParseArgumentSource(item.TryGetProperty("source", out var s) ? s.GetString() : null),
                        Relevance: item.TryGetProperty("relevance", out var r) ? r.GetDouble() : 0.5
                    ));
                }
            }

            // Add history-based suggestions
            foreach (var prev in previousArgs.Take(3))
            {
                if (!suggestions.Any(s => s.Value == prev))
                {
                    suggestions.Add(new ArgumentSuggestion(prev, "Previously used", ArgumentSource.History, 0.7));
                }
            }

            return new ArgumentSuggestions(
                Suggestions: suggestions.OrderByDescending(s => s.Relevance).ToList(),
                CurrentArgDescription: root.TryGetProperty("currentArgDescription", out var desc)
                    ? desc.GetString()
                    : null
            );
        }
        catch
        {
            // Fallback to history-only suggestions
            return new ArgumentSuggestions(
                previousArgs.Select(p => new ArgumentSuggestion(p, "Previously used", ArgumentSource.History, 0.7)).ToList(),
                null
            );
        }
    }

    private static ScriptDocumentation ParseDocumentation(string response, string scriptPath)
    {
        try
        {
            var json = ExtractJson(response);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var parameters = new List<ParameterDoc>();
            if (root.TryGetProperty("parameters", out var paramsArr))
            {
                foreach (var item in paramsArr.EnumerateArray())
                {
                    parameters.Add(new ParameterDoc(
                        Name: item.GetProperty("name").GetString() ?? "param",
                        Description: item.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                        Type: item.TryGetProperty("type", out var t) ? t.GetString() : null,
                        Required: item.TryGetProperty("required", out var r) && r.GetBoolean(),
                        DefaultValue: item.TryGetProperty("defaultValue", out var dv) ? dv.GetString() : null
                    ));
                }
            }

            return new ScriptDocumentation(
                Title: root.TryGetProperty("title", out var title)
                    ? title.GetString() ?? Path.GetFileNameWithoutExtension(scriptPath)
                    : Path.GetFileNameWithoutExtension(scriptPath),
                Description: root.TryGetProperty("description", out var desc)
                    ? desc.GetString() ?? "No description available"
                    : "No description available",
                Parameters: parameters,
                Examples: ParseStringArray(root, "examples"),
                Prerequisites: ParseStringArray(root, "prerequisites"),
                Notes: root.TryGetProperty("notes", out var notes) ? notes.GetString() : null
            );
        }
        catch
        {
            return new ScriptDocumentation(
                Path.GetFileNameWithoutExtension(scriptPath),
                "Documentation generation failed",
                Array.Empty<ParameterDoc>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                null
            );
        }
    }

    #endregion

    #region Helpers

    private static string ExtractJson(string response)
    {
        // Try to extract JSON from response (handle markdown code blocks)
        var start = response.IndexOf('{');
        var end = response.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return response[start..(end + 1)];
        }
        return response;
    }

    private static string TruncateOutput(string output, int maxLength)
    {
        if (string.IsNullOrEmpty(output)) return "";
        if (output.Length <= maxLength) return output;
        return output[..maxLength] + "\n... (truncated)";
    }

    private static string ComputeHash(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash)[..16];
    }

    private static ErrorSeverity ParseSeverity(string? value) => value?.ToLowerInvariant() switch
    {
        "info" => ErrorSeverity.Info,
        "warning" => ErrorSeverity.Warning,
        "critical" => ErrorSeverity.Critical,
        _ => ErrorSeverity.Error
    };

    private static FixType ParseFixType(string? value) => value switch
    {
        "ArgumentChange" => FixType.ArgumentChange,
        "EnvironmentChange" => FixType.EnvironmentChange,
        "DependencyInstall" => FixType.DependencyInstall,
        "PermissionFix" => FixType.PermissionFix,
        "ConfigurationChange" => FixType.ConfigurationChange,
        _ => FixType.CodeChange
    };

    private static ArgumentSource ParseArgumentSource(string? value) => value switch
    {
        "History" => ArgumentSource.History,
        "AIGenerated" => ArgumentSource.AIGenerated,
        "DefaultValue" => ArgumentSource.DefaultValue,
        _ => ArgumentSource.ScriptAnalysis
    };

    private static IReadOnlyList<string> ParseStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var arr)) return Array.Empty<string>();
        var result = new List<string>();
        foreach (var item in arr.EnumerateArray())
        {
            var str = item.GetString();
            if (!string.IsNullOrEmpty(str)) result.Add(str);
        }
        return result;
    }

    private bool TryGetCached<T>(string key, out T? value)
    {
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(key, out var cached) &&
                cached.ExpiresAt > DateTimeOffset.UtcNow)
            {
                value = (T)cached.Value;
                return true;
            }
            value = default;
            return false;
        }
    }

    private void CacheResponse<T>(string key, T value)
    {
        lock (_cacheLock)
        {
            _cache[key] = new CachedResponse(value!, DateTimeOffset.UtcNow.Add(_options.CacheDuration));

            // Cleanup old entries
            if (_cache.Count > 100)
            {
                var expired = _cache.Where(kv => kv.Value.ExpiresAt < DateTimeOffset.UtcNow)
                    .Select(kv => kv.Key).ToList();
                foreach (var k in expired) _cache.Remove(k);
            }
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _rateLimiter.Dispose();
    }

    #endregion

    private sealed record CachedResponse(object Value, DateTimeOffset ExpiresAt);
}

/// <summary>
/// Configuration options for the AI assistant.
/// </summary>
public sealed class AIAssistantOptions
{
    public string OllamaBaseUrl { get; init; } = "http://localhost:11434";
    public string Model { get; init; } = "llama3";
    public double Temperature { get; init; } = 0.3;
    public double TopP { get; init; } = 0.9;
    public int TimeoutSeconds { get; init; } = 60;
    public int MaxConcurrentRequests { get; init; } = 2;
    public TimeSpan CacheDuration { get; init; } = TimeSpan.FromMinutes(15);
}

#region Ollama API Models

internal sealed class OllamaGenerateRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("prompt")]
    public required string Prompt { get; init; }

    [JsonPropertyName("stream")]
    public bool Stream { get; init; }

    [JsonPropertyName("options")]
    public OllamaOptions? Options { get; init; }
}

internal sealed class OllamaOptions
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; init; }

    [JsonPropertyName("top_p")]
    public double TopP { get; init; }
}

internal sealed class OllamaGenerateResponse
{
    [JsonPropertyName("response")]
    public string? Response { get; init; }

    [JsonPropertyName("done")]
    public bool Done { get; init; }
}

#endregion
