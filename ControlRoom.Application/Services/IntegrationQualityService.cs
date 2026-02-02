using System.Collections.Concurrent;
using ControlRoom.Domain.Model;
using DomainIntegrationCategory = ControlRoom.Domain.Model.IntegrationCategory;

namespace ControlRoom.Application.Services;

/// <summary>
/// Integration Quality: Ensures all external integrations meet quality standards
/// for setup, runtime, failure handling, and removal.
///
/// Checklist items addressed:
/// - Clear description of value
/// - Permissions/scopes explained
/// - Setup succeeds offline-safe where possible
/// - Health status visible
/// - Auth refresh handled
/// - Rate limits respected
/// - Errors mapped to friendly messages
/// - Safe reconnect path
/// - No silent data loss
/// - Safe disconnect
/// - User understands impact
/// </summary>
public sealed class IntegrationQualityService
{
    private readonly IIntegrationQualityRepository _repository;
    private readonly ErrorHandlingService _errorHandler;
    private readonly ConcurrentDictionary<string, IntegrationHealthInfo> _healthCache = new();
    private readonly ConcurrentDictionary<string, RateLimitState> _rateLimitStates = new();
    private readonly ConcurrentDictionary<string, TokenRefreshState> _tokenStates = new();

    public IntegrationQualityService(
        IIntegrationQualityRepository repository,
        ErrorHandlingService errorHandler)
    {
        _repository = repository;
        _errorHandler = errorHandler;
    }

    // ========================================================================
    // SETUP: Clear Value Description & Permissions
    // ========================================================================

    /// <summary>
    /// Gets the integration catalog with clear value descriptions.
    /// </summary>
    public IReadOnlyList<IntegrationCatalogEntry> GetIntegrationCatalog()
    {
        return
        [
            new IntegrationCatalogEntry(
                Id: "github",
                Name: "GitHub",
                Category: DomainIntegrationCategory.SourceControl,
                ValueProposition: "Automatically sync deployments with commits, link issues to incidents, and trigger workflows from code changes.",
                RequiredScopes: ["repo:read", "repo:status", "workflow:trigger"],
                OptionalScopes: ["repo:write", "issues:write"],
                ScopeExplanations: new Dictionary<string, string>
                {
                    ["repo:read"] = "View repository contents and commit history",
                    ["repo:status"] = "Read and set commit statuses for CI integration",
                    ["workflow:trigger"] = "Trigger GitHub Actions workflows",
                    ["repo:write"] = "Create branches and commits (optional, for automation)",
                    ["issues:write"] = "Create and update issues (optional, for incident linking)"
                },
                SupportsOfflineSetup: true,
                SetupComplexity: SetupComplexity.Simple),

            new IntegrationCatalogEntry(
                Id: "azure-devops",
                Name: "Azure DevOps",
                Category: DomainIntegrationCategory.SourceControl,
                ValueProposition: "Connect pipelines to deployments, sync work items with incidents, and monitor build health.",
                RequiredScopes: ["vso.code", "vso.build"],
                OptionalScopes: ["vso.work_write", "vso.release"],
                ScopeExplanations: new Dictionary<string, string>
                {
                    ["vso.code"] = "Read code and repository metadata",
                    ["vso.build"] = "Access build definitions and results",
                    ["vso.work_write"] = "Create and update work items (optional)",
                    ["vso.release"] = "Access release pipelines (optional)"
                },
                SupportsOfflineSetup: false,
                SetupComplexity: SetupComplexity.Moderate),

            new IntegrationCatalogEntry(
                Id: "pagerduty",
                Name: "PagerDuty",
                Category: DomainIntegrationCategory.Alerting,
                ValueProposition: "Centralize incident management, escalate alerts automatically, and track on-call schedules.",
                RequiredScopes: ["incidents:read", "schedules:read"],
                OptionalScopes: ["incidents:write", "escalation_policies:read"],
                ScopeExplanations: new Dictionary<string, string>
                {
                    ["incidents:read"] = "View active and historical incidents",
                    ["schedules:read"] = "View on-call schedules",
                    ["incidents:write"] = "Create and acknowledge incidents (optional)",
                    ["escalation_policies:read"] = "View escalation rules (optional)"
                },
                SupportsOfflineSetup: false,
                SetupComplexity: SetupComplexity.Simple),

            new IntegrationCatalogEntry(
                Id: "datadog",
                Name: "Datadog",
                Category: DomainIntegrationCategory.Monitoring,
                ValueProposition: "Correlate deployment events with metrics, embed dashboards, and trigger alerts from anomalies.",
                RequiredScopes: ["metrics:read", "dashboards:read"],
                OptionalScopes: ["events:write", "monitors:read"],
                ScopeExplanations: new Dictionary<string, string>
                {
                    ["metrics:read"] = "Query metrics for dashboards and alerts",
                    ["dashboards:read"] = "Embed existing dashboards",
                    ["events:write"] = "Post deployment events (optional)",
                    ["monitors:read"] = "View alert configurations (optional)"
                },
                SupportsOfflineSetup: true,
                SetupComplexity: SetupComplexity.Simple),

            new IntegrationCatalogEntry(
                Id: "slack",
                Name: "Slack",
                Category: DomainIntegrationCategory.Communication,
                ValueProposition: "Send deployment notifications, alert on incidents, and enable ChatOps workflows.",
                RequiredScopes: ["chat:write", "channels:read"],
                OptionalScopes: ["commands", "users:read"],
                ScopeExplanations: new Dictionary<string, string>
                {
                    ["chat:write"] = "Post messages to channels",
                    ["channels:read"] = "List available channels for selection",
                    ["commands"] = "Respond to slash commands (optional)",
                    ["users:read"] = "Show user names in notifications (optional)"
                },
                SupportsOfflineSetup: false,
                SetupComplexity: SetupComplexity.Simple),

            new IntegrationCatalogEntry(
                Id: "kubernetes",
                Name: "Kubernetes",
                Category: DomainIntegrationCategory.CloudProvider,
                ValueProposition: "Monitor cluster health, manage deployments, and track resource usage across environments.",
                RequiredScopes: ["get:pods", "list:deployments", "get:services"],
                OptionalScopes: ["create:deployments", "delete:pods"],
                ScopeExplanations: new Dictionary<string, string>
                {
                    ["get:pods"] = "View pod status and logs",
                    ["list:deployments"] = "List deployment configurations",
                    ["get:services"] = "View service endpoints",
                    ["create:deployments"] = "Deploy new versions (optional)",
                    ["delete:pods"] = "Restart unhealthy pods (optional)"
                },
                SupportsOfflineSetup: true,
                SetupComplexity: SetupComplexity.Complex)
        ];
    }

    /// <summary>
    /// Validates integration setup configuration.
    /// </summary>
    public async Task<IntegrationSetupValidation> ValidateSetupAsync(
        string integrationId,
        IntegrationSetupConfig config,
        CancellationToken cancellationToken = default)
    {
        var catalog = GetIntegrationCatalog().FirstOrDefault(c => c.Id == integrationId);
        if (catalog == null)
        {
            return new IntegrationSetupValidation(
                IsValid: false,
                Errors: ["Unknown integration type"],
                Warnings: [],
                MissingRequiredScopes: [],
                MissingOptionalScopes: []);
        }

        var errors = new List<string>();
        var warnings = new List<string>();

        // Check required scopes
        var missingRequired = catalog.RequiredScopes
            .Where(s => !config.GrantedScopes.Contains(s))
            .ToList();

        if (missingRequired.Count > 0)
        {
            foreach (var scope in missingRequired)
            {
                var explanation = catalog.ScopeExplanations.GetValueOrDefault(scope, scope);
                errors.Add($"Missing required permission: {explanation}");
            }
        }

        // Check optional scopes
        var missingOptional = catalog.OptionalScopes
            .Where(s => !config.GrantedScopes.Contains(s))
            .ToList();

        foreach (var scope in missingOptional)
        {
            var explanation = catalog.ScopeExplanations.GetValueOrDefault(scope, scope);
            warnings.Add($"Optional permission not granted: {explanation}");
        }

        // Validate connectivity if online
        if (config.ValidateConnectivity && !await IsOfflineAsync(cancellationToken))
        {
            try
            {
                var connected = await _repository.TestConnectionAsync(integrationId, config, cancellationToken);
                if (!connected)
                {
                    errors.Add("Could not connect to the service. Check your credentials and try again.");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Connection test failed: {ex.Message}");
            }
        }
        else if (!catalog.SupportsOfflineSetup)
        {
            warnings.Add("This integration cannot be fully validated offline. Verification will complete when online.");
        }

        return new IntegrationSetupValidation(
            IsValid: errors.Count == 0,
            Errors: errors,
            Warnings: warnings,
            MissingRequiredScopes: missingRequired,
            MissingOptionalScopes: missingOptional);
    }

    // ========================================================================
    // RUNTIME: Health Status & Auth Refresh
    // ========================================================================

    /// <summary>
    /// Gets the health status of all active integrations.
    /// </summary>
    public async Task<IReadOnlyList<IntegrationHealthInfo>> GetIntegrationHealthAsync(
        CancellationToken cancellationToken = default)
    {
        var integrations = await _repository.GetActiveIntegrationsAsync(cancellationToken);
        var healthResults = new List<IntegrationHealthInfo>();

        foreach (var integration in integrations)
        {
            var health = await CheckIntegrationHealthAsync(integration, cancellationToken);
            _healthCache[integration.Id] = health;
            healthResults.Add(health);
        }

        return healthResults;
    }

    /// <summary>
    /// Gets cached health status (non-blocking).
    /// </summary>
    public IntegrationHealthInfo? GetCachedHealth(string integrationId)
    {
        return _healthCache.TryGetValue(integrationId, out var health) ? health : null;
    }

    /// <summary>
    /// Handles token refresh for OAuth integrations.
    /// </summary>
    public async Task<TokenRefreshResult> RefreshTokenAsync(
        string integrationId,
        CancellationToken cancellationToken = default)
    {
        var integration = await _repository.GetIntegrationAsync(integrationId, cancellationToken);
        if (integration == null)
        {
            return new TokenRefreshResult(
                Success: false,
                Error: "Integration not found",
                NewExpiresAt: null);
        }

        if (!integration.SupportsOAuth)
        {
            return new TokenRefreshResult(
                Success: true,
                Error: null,
                NewExpiresAt: null);
        }

        // Track refresh state to prevent concurrent refreshes
        var state = _tokenStates.GetOrAdd(integrationId, _ => new TokenRefreshState());

        await state.RefreshLock.WaitAsync(cancellationToken);
        try
        {
            // Check if another thread just refreshed
            if (state.LastRefresh.HasValue &&
                DateTimeOffset.UtcNow - state.LastRefresh.Value < TimeSpan.FromSeconds(30))
            {
                return new TokenRefreshResult(
                    Success: true,
                    Error: null,
                    NewExpiresAt: state.ExpiresAt);
            }

            try
            {
                var result = await _repository.RefreshOAuthTokenAsync(integrationId, cancellationToken);
                state.LastRefresh = DateTimeOffset.UtcNow;
                state.ExpiresAt = result.ExpiresAt;
                state.ConsecutiveFailures = 0;

                return new TokenRefreshResult(
                    Success: true,
                    Error: null,
                    NewExpiresAt: result.ExpiresAt);
            }
            catch (Exception ex)
            {
                state.ConsecutiveFailures++;

                // After 3 failures, mark as needing reauthorization
                if (state.ConsecutiveFailures >= 3)
                {
                    await _repository.MarkNeedsReauthorizationAsync(integrationId, cancellationToken);
                }

                var error = _errorHandler.CreateConnectionError(integrationId, false, ex);
                return new TokenRefreshResult(
                    Success: false,
                    Error: error.Message,
                    NewExpiresAt: null);
            }
        }
        finally
        {
            state.RefreshLock.Release();
        }
    }

    /// <summary>
    /// Proactively refreshes tokens expiring soon.
    /// </summary>
    public async Task<int> RefreshExpiringTokensAsync(
        TimeSpan expirationWindow,
        CancellationToken cancellationToken = default)
    {
        var expiring = await _repository.GetExpiringIntegrationsAsync(expirationWindow, cancellationToken);
        var refreshedCount = 0;

        foreach (var integration in expiring)
        {
            var result = await RefreshTokenAsync(integration.Id, cancellationToken);
            if (result.Success)
            {
                refreshedCount++;
            }
        }

        return refreshedCount;
    }

    // ========================================================================
    // RUNTIME: Rate Limiting
    // ========================================================================

    /// <summary>
    /// Checks if a request can proceed under rate limits.
    /// </summary>
    public async Task<RateLimitDecision> CheckRateLimitAsync(
        string integrationId,
        string operation,
        CancellationToken cancellationToken = default)
    {
        var key = $"{integrationId}:{operation}";
        var state = _rateLimitStates.GetOrAdd(key, _ => new RateLimitState());

        // Check if we're in backoff period
        if (state.BackoffUntil.HasValue && DateTimeOffset.UtcNow < state.BackoffUntil.Value)
        {
            var waitTime = state.BackoffUntil.Value - DateTimeOffset.UtcNow;
            return new RateLimitDecision(
                Allowed: false,
                RetryAfter: waitTime,
                RemainingRequests: 0,
                Message: $"Rate limited. Retry in {waitTime.TotalSeconds:F0} seconds.");
        }

        // Get current limits
        var limits = await _repository.GetRateLimitsAsync(integrationId, cancellationToken);
        if (limits == null)
        {
            return new RateLimitDecision(
                Allowed: true,
                RetryAfter: null,
                RemainingRequests: null,
                Message: null);
        }

        // Check against limits
        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddSeconds(-limits.WindowSeconds);

        // Clean old entries
        while (state.RequestTimestamps.TryPeek(out var oldest) && oldest < windowStart)
        {
            state.RequestTimestamps.TryDequeue(out _);
        }

        var currentCount = state.RequestTimestamps.Count;
        var remaining = Math.Max(0, limits.MaxRequests - currentCount);

        if (currentCount >= limits.MaxRequests)
        {
            // Calculate when the oldest request will fall out of the window
            if (state.RequestTimestamps.TryPeek(out var oldestInWindow))
            {
                var retryAfter = oldestInWindow.AddSeconds(limits.WindowSeconds) - now;
                state.BackoffUntil = now.Add(retryAfter);

                return new RateLimitDecision(
                    Allowed: false,
                    RetryAfter: retryAfter,
                    RemainingRequests: 0,
                    Message: $"Rate limit reached ({limits.MaxRequests} requests per {limits.WindowSeconds}s). Retry in {retryAfter.TotalSeconds:F0}s.");
            }
        }

        return new RateLimitDecision(
            Allowed: true,
            RetryAfter: null,
            RemainingRequests: remaining,
            Message: null);
    }

    /// <summary>
    /// Records a request for rate limiting purposes.
    /// </summary>
    public void RecordRequest(string integrationId, string operation)
    {
        var key = $"{integrationId}:{operation}";
        var state = _rateLimitStates.GetOrAdd(key, _ => new RateLimitState());
        state.RequestTimestamps.Enqueue(DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Records a rate limit response from the API.
    /// </summary>
    public void RecordRateLimitResponse(
        string integrationId,
        string operation,
        TimeSpan retryAfter)
    {
        var key = $"{integrationId}:{operation}";
        var state = _rateLimitStates.GetOrAdd(key, _ => new RateLimitState());
        state.BackoffUntil = DateTimeOffset.UtcNow.Add(retryAfter);
    }

    // ========================================================================
    // FAILURE HANDLING: Friendly Errors & Reconnection
    // ========================================================================

    /// <summary>
    /// Maps an integration error to a friendly message.
    /// </summary>
    public FriendlyError MapIntegrationError(
        string integrationId,
        Exception exception)
    {
        var catalog = GetIntegrationCatalog().FirstOrDefault(c => c.Id == integrationId);
        var integrationName = catalog?.Name ?? integrationId;
        var context = new ErrorContext(ErrorSource.ExternalService, integrationId);

        return exception switch
        {
            UnauthorizedAccessException => _errorHandler.CreateAuthError(
                integrationName,
                AuthErrorReason.Expired),

            HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests } =>
                _errorHandler.CreateRateLimitError(
                    integrationName,
                    TimeSpan.FromMinutes(1)),

            HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized } =>
                _errorHandler.CreateAuthError(
                    integrationName,
                    AuthErrorReason.Revoked),

            HttpRequestException httpEx when httpEx.StatusCode >= System.Net.HttpStatusCode.InternalServerError =>
                _errorHandler.CreateConnectionError(integrationName, false, exception),

            TaskCanceledException => _errorHandler.CreateConnectionError(integrationName, false, exception),

            _ => _errorHandler.CreateFriendlyError(exception, context)
        };
    }

    /// <summary>
    /// Attempts to safely reconnect an integration.
    /// </summary>
    public async Task<ReconnectResult> ReconnectAsync(
        string integrationId,
        CancellationToken cancellationToken = default)
    {
        var integration = await _repository.GetIntegrationAsync(integrationId, cancellationToken);
        if (integration == null)
        {
            return new ReconnectResult(
                Success: false,
                RequiresReauthorization: false,
                Error: "Integration not found",
                RecoveredData: null);
        }

        // Step 1: Try token refresh if applicable
        if (integration.SupportsOAuth)
        {
            var refreshResult = await RefreshTokenAsync(integrationId, cancellationToken);
            if (refreshResult.Success)
            {
                // Verify connection works
                var health = await CheckIntegrationHealthAsync(integration, cancellationToken);
                if (health.Status == IntegrationHealthStatusType.Healthy)
                {
                    return new ReconnectResult(
                        Success: true,
                        RequiresReauthorization: false,
                        Error: null,
                        RecoveredData: null);
                }
            }
        }

        // Step 2: Check if we need full reauthorization
        var needsReauth = await _repository.NeedsReauthorizationAsync(integrationId, cancellationToken);
        if (needsReauth)
        {
            return new ReconnectResult(
                Success: false,
                RequiresReauthorization: true,
                Error: "Integration requires you to sign in again.",
                RecoveredData: null);
        }

        // Step 3: Try simple reconnection
        try
        {
            await _repository.ReconnectAsync(integrationId, cancellationToken);

            var health = await CheckIntegrationHealthAsync(integration, cancellationToken);
            return new ReconnectResult(
                Success: health.Status == IntegrationHealthStatusType.Healthy,
                RequiresReauthorization: health.Status == IntegrationHealthStatusType.AuthError,
                Error: health.Status != IntegrationHealthStatusType.Healthy ? health.StatusMessage : null,
                RecoveredData: null);
        }
        catch (Exception ex)
        {
            return new ReconnectResult(
                Success: false,
                RequiresReauthorization: false,
                Error: ex.Message,
                RecoveredData: null);
        }
    }

    // ========================================================================
    // FAILURE HANDLING: Data Loss Prevention
    // ========================================================================

    /// <summary>
    /// Queues data that couldn't be sent due to integration failure.
    /// </summary>
    public async Task QueuePendingDataAsync(
        string integrationId,
        string operationType,
        object data,
        CancellationToken cancellationToken = default)
    {
        var entry = new PendingDataEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            IntegrationId = integrationId,
            OperationType = operationType,
            Data = System.Text.Json.JsonSerializer.Serialize(data),
            QueuedAt = DateTimeOffset.UtcNow,
            RetryCount = 0
        };

        await _repository.QueuePendingDataAsync(entry, cancellationToken);
    }

    /// <summary>
    /// Processes pending data when integration recovers.
    /// </summary>
    public async Task<DataRecoveryResult> ProcessPendingDataAsync(
        string integrationId,
        CancellationToken cancellationToken = default)
    {
        var pending = await _repository.GetPendingDataAsync(integrationId, cancellationToken);
        var processed = 0;
        var failed = 0;
        var errors = new List<string>();

        foreach (var entry in pending)
        {
            try
            {
                await _repository.ProcessPendingEntryAsync(entry, cancellationToken);
                await _repository.RemovePendingDataAsync(entry.Id, cancellationToken);
                processed++;
            }
            catch (Exception ex)
            {
                failed++;
                entry.RetryCount++;
                entry.LastError = ex.Message;
                await _repository.UpdatePendingDataAsync(entry, cancellationToken);

                if (entry.RetryCount >= 5)
                {
                    errors.Add($"Failed to process {entry.OperationType}: {ex.Message}");
                }
            }
        }

        return new DataRecoveryResult(
            ProcessedCount: processed,
            FailedCount: failed,
            RemainingCount: pending.Count - processed,
            Errors: errors);
    }

    // ========================================================================
    // REMOVAL: Safe Disconnect
    // ========================================================================

    /// <summary>
    /// Gets impact assessment for removing an integration.
    /// </summary>
    public async Task<DisconnectImpact> GetDisconnectImpactAsync(
        string integrationId,
        CancellationToken cancellationToken = default)
    {
        var catalog = GetIntegrationCatalog().FirstOrDefault(c => c.Id == integrationId);
        var integrationName = catalog?.Name ?? integrationId;

        var dependencies = await _repository.GetDependentFeaturesAsync(integrationId, cancellationToken);
        var pendingData = await _repository.GetPendingDataCountAsync(integrationId, cancellationToken);
        var activeWorkflows = await _repository.GetActiveWorkflowsAsync(integrationId, cancellationToken);

        var impacts = new List<string>();
        var warnings = new List<string>();

        if (dependencies.Count > 0)
        {
            impacts.Add($"These features will stop working: {string.Join(", ", dependencies)}");
        }

        if (pendingData > 0)
        {
            warnings.Add($"{pendingData} pending operations will be lost");
        }

        if (activeWorkflows.Count > 0)
        {
            warnings.Add($"{activeWorkflows.Count} active workflows will be disabled");
        }

        var impactSeverity = (dependencies.Count, pendingData, activeWorkflows.Count) switch
        {
            ( > 5, _, _) => DisconnectSeverity.High,
            (_, > 10, _) => DisconnectSeverity.High,
            (_, _, > 3) => DisconnectSeverity.Medium,
            ( > 0, _, _) => DisconnectSeverity.Medium,
            _ => DisconnectSeverity.Low
        };

        return new DisconnectImpact(
            IntegrationId: integrationId,
            IntegrationName: integrationName,
            Severity: impactSeverity,
            AffectedFeatures: dependencies,
            PendingDataCount: pendingData,
            ActiveWorkflows: activeWorkflows,
            Impacts: impacts,
            Warnings: warnings,
            CanUndo: true,
            UndoWindow: TimeSpan.FromDays(30));
    }

    /// <summary>
    /// Safely disconnects an integration with user confirmation.
    /// </summary>
    public async Task<DisconnectResult> DisconnectAsync(
        string integrationId,
        DisconnectOptions options,
        CancellationToken cancellationToken = default)
    {
        var impact = await GetDisconnectImpactAsync(integrationId, cancellationToken);

        // Require explicit confirmation for high severity
        if (impact.Severity == DisconnectSeverity.High && !options.ConfirmHighImpact)
        {
            return new DisconnectResult(
                Success: false,
                RequiresConfirmation: true,
                Impact: impact,
                Error: "This integration has significant dependencies. Please confirm you understand the impact.");
        }

        try
        {
            // Archive pending data if requested
            if (options.ArchivePendingData && impact.PendingDataCount > 0)
            {
                await _repository.ArchivePendingDataAsync(integrationId, cancellationToken);
            }

            // Disable dependent workflows
            foreach (var workflow in impact.ActiveWorkflows)
            {
                await _repository.DisableWorkflowAsync(workflow.Id, cancellationToken);
            }

            // Create undo snapshot
            if (options.AllowUndo)
            {
                await _repository.CreateDisconnectSnapshotAsync(integrationId, cancellationToken);
            }

            // Revoke tokens and clear credentials
            await _repository.RevokeIntegrationAsync(integrationId, cancellationToken);

            // Clear cached state
            _healthCache.TryRemove(integrationId, out _);

            return new DisconnectResult(
                Success: true,
                RequiresConfirmation: false,
                Impact: impact,
                Error: null);
        }
        catch (Exception ex)
        {
            return new DisconnectResult(
                Success: false,
                RequiresConfirmation: false,
                Impact: impact,
                Error: ex.Message);
        }
    }

    /// <summary>
    /// Undoes a recent disconnect if within the undo window.
    /// </summary>
    public async Task<ReconnectResult> UndoDisconnectAsync(
        string integrationId,
        CancellationToken cancellationToken = default)
    {
        var hasSnapshot = await _repository.HasDisconnectSnapshotAsync(integrationId, cancellationToken);
        if (!hasSnapshot)
        {
            return new ReconnectResult(
                Success: false,
                RequiresReauthorization: true,
                Error: "No recent disconnect snapshot found. You'll need to set up this integration again.",
                RecoveredData: null);
        }

        try
        {
            await _repository.RestoreFromSnapshotAsync(integrationId, cancellationToken);

            // Verify restoration
            var integration = await _repository.GetIntegrationAsync(integrationId, cancellationToken);
            if (integration == null)
            {
                return new ReconnectResult(
                    Success: false,
                    RequiresReauthorization: true,
                    Error: "Failed to restore integration.",
                    RecoveredData: null);
            }

            var health = await CheckIntegrationHealthAsync(integration, cancellationToken);

            return new ReconnectResult(
                Success: health.Status == IntegrationHealthStatusType.Healthy,
                RequiresReauthorization: health.Status == IntegrationHealthStatusType.AuthError,
                Error: health.Status != IntegrationHealthStatusType.Healthy ? health.StatusMessage : null,
                RecoveredData: null);
        }
        catch (Exception ex)
        {
            return new ReconnectResult(
                Success: false,
                RequiresReauthorization: true,
                Error: ex.Message,
                RecoveredData: null);
        }
    }

    // ========================================================================
    // Private Helpers
    // ========================================================================

    private async Task<IntegrationHealthInfo> CheckIntegrationHealthAsync(
        IntegrationInfo integration,
        CancellationToken cancellationToken)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            // Check token expiry
            if (integration.SupportsOAuth && integration.TokenExpiresAt.HasValue)
            {
                if (integration.TokenExpiresAt.Value < DateTimeOffset.UtcNow)
                {
                    return new IntegrationHealthInfo(
                        IntegrationId: integration.Id,
                        Status: IntegrationHealthStatusType.AuthError,
                        StatusMessage: "Authentication token has expired",
                        LastChecked: startTime,
                        ResponseTime: DateTimeOffset.UtcNow - startTime,
                        LastSuccessfulSync: integration.LastSyncAt);
                }

                // Warn if expiring soon
                if (integration.TokenExpiresAt.Value < DateTimeOffset.UtcNow.AddHours(1))
                {
                    return new IntegrationHealthInfo(
                        IntegrationId: integration.Id,
                        Status: IntegrationHealthStatusType.Warning,
                        StatusMessage: "Authentication token expiring soon",
                        LastChecked: startTime,
                        ResponseTime: DateTimeOffset.UtcNow - startTime,
                        LastSuccessfulSync: integration.LastSyncAt);
                }
            }

            // Ping the service
            var isReachable = await _repository.PingIntegrationAsync(integration.Id, cancellationToken);

            return new IntegrationHealthInfo(
                IntegrationId: integration.Id,
                Status: isReachable ? IntegrationHealthStatusType.Healthy : IntegrationHealthStatusType.Unreachable,
                StatusMessage: isReachable ? "Connected" : "Service unreachable",
                LastChecked: DateTimeOffset.UtcNow,
                ResponseTime: DateTimeOffset.UtcNow - startTime,
                LastSuccessfulSync: integration.LastSyncAt);
        }
        catch (UnauthorizedAccessException)
        {
            return new IntegrationHealthInfo(
                IntegrationId: integration.Id,
                Status: IntegrationHealthStatusType.AuthError,
                StatusMessage: "Authentication failed",
                LastChecked: DateTimeOffset.UtcNow,
                ResponseTime: DateTimeOffset.UtcNow - startTime,
                LastSuccessfulSync: integration.LastSyncAt);
        }
        catch (Exception ex)
        {
            return new IntegrationHealthInfo(
                IntegrationId: integration.Id,
                Status: IntegrationHealthStatusType.Error,
                StatusMessage: ex.Message,
                LastChecked: DateTimeOffset.UtcNow,
                ResponseTime: DateTimeOffset.UtcNow - startTime,
                LastSuccessfulSync: integration.LastSyncAt);
        }
    }

    private async Task<bool> IsOfflineAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Simple connectivity check
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            return !await _repository.CheckConnectivityAsync(cts.Token);
        }
        catch
        {
            return true;
        }
    }
}

// ============================================================================
// Integration Quality Types
// ============================================================================

/// <summary>
/// Integration catalog entry with clear value description.
/// </summary>
public sealed record IntegrationCatalogEntry(
    string Id,
    string Name,
    DomainIntegrationCategory Category,
    string ValueProposition,
    IReadOnlyList<string> RequiredScopes,
    IReadOnlyList<string> OptionalScopes,
    IReadOnlyDictionary<string, string> ScopeExplanations,
    bool SupportsOfflineSetup,
    SetupComplexity SetupComplexity);

/// <summary>
/// Setup complexity level.
/// </summary>
public enum SetupComplexity
{
    Simple,
    Moderate,
    Complex
}

/// <summary>
/// Integration setup configuration.
/// </summary>
public sealed record IntegrationSetupConfig(
    IReadOnlyList<string> GrantedScopes,
    bool ValidateConnectivity = true,
    Dictionary<string, string>? CustomSettings = null);

/// <summary>
/// Setup validation result.
/// </summary>
public sealed record IntegrationSetupValidation(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> MissingRequiredScopes,
    IReadOnlyList<string> MissingOptionalScopes);

/// <summary>
/// Integration health info.
/// </summary>
public sealed record IntegrationHealthInfo(
    string IntegrationId,
    IntegrationHealthStatusType Status,
    string StatusMessage,
    DateTimeOffset LastChecked,
    TimeSpan ResponseTime,
    DateTimeOffset? LastSuccessfulSync);

/// <summary>
/// Health status enumeration.
/// </summary>
public enum IntegrationHealthStatusType
{
    Healthy,
    Warning,
    Degraded,
    Unreachable,
    AuthError,
    Error,
    Unknown
}

/// <summary>
/// Token refresh result.
/// </summary>
public sealed record TokenRefreshResult(
    bool Success,
    string? Error,
    DateTimeOffset? NewExpiresAt);

/// <summary>
/// Token refresh state tracker.
/// </summary>
internal sealed class TokenRefreshState
{
    public SemaphoreSlim RefreshLock { get; } = new(1, 1);
    public DateTimeOffset? LastRefresh { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public int ConsecutiveFailures { get; set; }
}

/// <summary>
/// Rate limit state tracker.
/// </summary>
internal sealed class RateLimitState
{
    public ConcurrentQueue<DateTimeOffset> RequestTimestamps { get; } = new();
    public DateTimeOffset? BackoffUntil { get; set; }
}

/// <summary>
/// Rate limit decision.
/// </summary>
public sealed record RateLimitDecision(
    bool Allowed,
    TimeSpan? RetryAfter,
    int? RemainingRequests,
    string? Message);

/// <summary>
/// Reconnect result.
/// </summary>
public sealed record ReconnectResult(
    bool Success,
    bool RequiresReauthorization,
    string? Error,
    object? RecoveredData);

/// <summary>
/// Pending data entry for retry.
/// </summary>
public sealed class PendingDataEntry
{
    public required string Id { get; set; }
    public required string IntegrationId { get; set; }
    public required string OperationType { get; set; }
    public required string Data { get; set; }
    public required DateTimeOffset QueuedAt { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}

/// <summary>
/// Data recovery result.
/// </summary>
public sealed record DataRecoveryResult(
    int ProcessedCount,
    int FailedCount,
    int RemainingCount,
    IReadOnlyList<string> Errors);

/// <summary>
/// Disconnect impact assessment.
/// </summary>
public sealed record DisconnectImpact(
    string IntegrationId,
    string IntegrationName,
    DisconnectSeverity Severity,
    IReadOnlyList<string> AffectedFeatures,
    int PendingDataCount,
    IReadOnlyList<WorkflowInfo> ActiveWorkflows,
    IReadOnlyList<string> Impacts,
    IReadOnlyList<string> Warnings,
    bool CanUndo,
    TimeSpan UndoWindow);

/// <summary>
/// Disconnect severity level.
/// </summary>
public enum DisconnectSeverity
{
    Low,
    Medium,
    High
}

/// <summary>
/// Workflow info.
/// </summary>
public sealed record WorkflowInfo(
    string Id,
    string Name,
    bool IsActive);

/// <summary>
/// Disconnect options.
/// </summary>
public sealed record DisconnectOptions(
    bool ConfirmHighImpact = false,
    bool ArchivePendingData = true,
    bool AllowUndo = true);

/// <summary>
/// Disconnect result.
/// </summary>
public sealed record DisconnectResult(
    bool Success,
    bool RequiresConfirmation,
    DisconnectImpact Impact,
    string? Error);

/// <summary>
/// Integration info model.
/// </summary>
public sealed class IntegrationInfo
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public bool SupportsOAuth { get; set; }
    public DateTimeOffset? TokenExpiresAt { get; set; }
    public DateTimeOffset? LastSyncAt { get; set; }
}

/// <summary>
/// Rate limits configuration.
/// </summary>
public sealed record RateLimits(
    int MaxRequests,
    int WindowSeconds);

/// <summary>
/// OAuth refresh result.
/// </summary>
public sealed record OAuthRefreshResult(
    DateTimeOffset ExpiresAt);

// ============================================================================
// Repository Interface
// ============================================================================

/// <summary>
/// Repository for integration quality data.
/// </summary>
public interface IIntegrationQualityRepository
{
    Task<bool> TestConnectionAsync(string integrationId, IntegrationSetupConfig config, CancellationToken cancellationToken);
    Task<IReadOnlyList<IntegrationInfo>> GetActiveIntegrationsAsync(CancellationToken cancellationToken);
    Task<IntegrationInfo?> GetIntegrationAsync(string id, CancellationToken cancellationToken);
    Task<OAuthRefreshResult> RefreshOAuthTokenAsync(string integrationId, CancellationToken cancellationToken);
    Task MarkNeedsReauthorizationAsync(string integrationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<IntegrationInfo>> GetExpiringIntegrationsAsync(TimeSpan window, CancellationToken cancellationToken);
    Task<RateLimits?> GetRateLimitsAsync(string integrationId, CancellationToken cancellationToken);
    Task<bool> NeedsReauthorizationAsync(string integrationId, CancellationToken cancellationToken);
    Task ReconnectAsync(string integrationId, CancellationToken cancellationToken);
    Task QueuePendingDataAsync(PendingDataEntry entry, CancellationToken cancellationToken);
    Task<IReadOnlyList<PendingDataEntry>> GetPendingDataAsync(string integrationId, CancellationToken cancellationToken);
    Task ProcessPendingEntryAsync(PendingDataEntry entry, CancellationToken cancellationToken);
    Task RemovePendingDataAsync(string entryId, CancellationToken cancellationToken);
    Task UpdatePendingDataAsync(PendingDataEntry entry, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetDependentFeaturesAsync(string integrationId, CancellationToken cancellationToken);
    Task<int> GetPendingDataCountAsync(string integrationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowInfo>> GetActiveWorkflowsAsync(string integrationId, CancellationToken cancellationToken);
    Task ArchivePendingDataAsync(string integrationId, CancellationToken cancellationToken);
    Task DisableWorkflowAsync(string workflowId, CancellationToken cancellationToken);
    Task CreateDisconnectSnapshotAsync(string integrationId, CancellationToken cancellationToken);
    Task RevokeIntegrationAsync(string integrationId, CancellationToken cancellationToken);
    Task<bool> HasDisconnectSnapshotAsync(string integrationId, CancellationToken cancellationToken);
    Task RestoreFromSnapshotAsync(string integrationId, CancellationToken cancellationToken);
    Task<bool> PingIntegrationAsync(string integrationId, CancellationToken cancellationToken);
    Task<bool> CheckConnectivityAsync(CancellationToken cancellationToken);
}
