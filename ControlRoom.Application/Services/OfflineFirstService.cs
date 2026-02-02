using System.Collections.Concurrent;
using System.Text.Json;

namespace ControlRoom.Application.Services;

/// <summary>
/// Offline-First: Ensures the app works reliably without network connectivity,
/// gracefully handles transitions, and syncs data transparently.
///
/// Checklist items addressed:
/// - Offline state clearly indicated
/// - No false error states
/// - Core workflows usable offline
/// - Actions queued transparently
/// - Sync resumes automatically
/// - Conflicts surfaced clearly
/// - User always knows where data lives
/// </summary>
public sealed class OfflineFirstService
{
    private readonly IOfflineStorageRepository _storage;
    private readonly IConnectivityMonitor _connectivityMonitor;
    private readonly ISyncEngine _syncEngine;
    private readonly ConcurrentQueue<QueuedAction> _actionQueue = new();
    private readonly ConcurrentDictionary<string, DataLocation> _dataLocations = new();

    private ConnectivityState _currentState = ConnectivityState.Unknown;
    private DateTimeOffset? _offlineSince;
    private bool _isSyncing;

    public event EventHandler<ConnectivityChangedEventArgs>? ConnectivityChanged;
    public event EventHandler<SyncProgressEventArgs>? SyncProgress;
    public event EventHandler<ConflictDetectedEventArgs>? ConflictDetected;
    public event EventHandler<DataLocationChangedEventArgs>? DataLocationChanged;

    public OfflineFirstService(
        IOfflineStorageRepository storage,
        IConnectivityMonitor connectivityMonitor,
        ISyncEngine syncEngine)
    {
        _storage = storage;
        _connectivityMonitor = connectivityMonitor;
        _syncEngine = syncEngine;

        _connectivityMonitor.ConnectivityChanged += OnConnectivityChanged;
    }

    // ========================================================================
    // DETECTION: Connectivity State Management
    // ========================================================================

    /// <summary>
    /// Gets the current connectivity state.
    /// </summary>
    public ConnectivityState CurrentState => _currentState;

    /// <summary>
    /// Gets whether the app is currently online.
    /// </summary>
    public bool IsOnline => _currentState == ConnectivityState.Online;

    /// <summary>
    /// Gets how long the app has been offline.
    /// </summary>
    public TimeSpan? OfflineDuration => _offlineSince.HasValue
        ? DateTimeOffset.UtcNow - _offlineSince.Value
        : null;

    /// <summary>
    /// Gets the current connectivity status with user-friendly message.
    /// </summary>
    public ConnectivityStatus GetStatus()
    {
        var (icon, message, color) = _currentState switch
        {
            ConnectivityState.Online => ("\uE701", "Online", StatusColor.Green),
            ConnectivityState.Offline => ("\uE702", GetOfflineMessage(), StatusColor.Yellow),
            ConnectivityState.LimitedConnectivity => ("\uE703", "Limited connectivity", StatusColor.Yellow),
            ConnectivityState.Syncing => ("\uE895", "Syncing...", StatusColor.Blue),
            _ => ("\uE704", "Checking connection...", StatusColor.Gray)
        };

        return new ConnectivityStatus(
            State: _currentState,
            Icon: icon,
            Message: message,
            Color: color,
            OfflineSince: _offlineSince,
            PendingActionsCount: _actionQueue.Count,
            CanWorkOffline: true);
    }

    /// <summary>
    /// Performs a connectivity check without triggering false errors.
    /// </summary>
    public async Task<ConnectivityCheckResult> CheckConnectivityAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var startTime = DateTimeOffset.UtcNow;
            var results = new List<EndpointCheckResult>();

            // Check multiple endpoints to avoid false negatives
            var endpoints = GetHealthCheckEndpoints();
            var successCount = 0;

            foreach (var endpoint in endpoints)
            {
                var result = await CheckEndpointAsync(endpoint, cancellationToken);
                results.Add(result);
                if (result.IsReachable)
                {
                    successCount++;
                }
            }

            // Require majority success to consider online (avoids false errors)
            var isOnline = successCount > endpoints.Count / 2;
            var latency = DateTimeOffset.UtcNow - startTime;

            var newState = isOnline
                ? ConnectivityState.Online
                : successCount > 0
                    ? ConnectivityState.LimitedConnectivity
                    : ConnectivityState.Offline;

            await UpdateStateAsync(newState);

            return new ConnectivityCheckResult(
                IsOnline: isOnline,
                State: newState,
                Latency: latency,
                EndpointResults: results,
                CheckedAt: DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            // Network check failed - don't treat as error, just report offline
            await UpdateStateAsync(ConnectivityState.Offline);

            return new ConnectivityCheckResult(
                IsOnline: false,
                State: ConnectivityState.Offline,
                Latency: null,
                EndpointResults: [],
                CheckedAt: DateTimeOffset.UtcNow,
                Error: ex.Message);
        }
    }

    // ========================================================================
    // FUNCTIONALITY: Offline-Capable Operations
    // ========================================================================

    /// <summary>
    /// Executes an operation with offline support.
    /// Returns immediately with local result, queues sync if offline.
    /// </summary>
    public async Task<OfflineOperationResult<T>> ExecuteAsync<T>(
        OfflineOperation<T> operation,
        CancellationToken cancellationToken = default)
    {
        // Always try local operation first
        T localResult;
        try
        {
            localResult = await operation.LocalExecute(cancellationToken);
            await _storage.SaveLocalDataAsync(operation.DataKey, localResult, cancellationToken);
            UpdateDataLocation(operation.DataKey, DataLocationState.Local);
        }
        catch (Exception ex)
        {
            return new OfflineOperationResult<T>(
                Success: false,
                Data: default,
                Source: DataSource.None,
                IsPending: false,
                Error: ex.Message);
        }

        // If online, try remote operation
        if (IsOnline && operation.RemoteExecute != null)
        {
            try
            {
                var remoteResult = await operation.RemoteExecute(cancellationToken);
                await _storage.SaveLocalDataAsync(operation.DataKey, remoteResult, cancellationToken);
                UpdateDataLocation(operation.DataKey, DataLocationState.Synced);

                return new OfflineOperationResult<T>(
                    Success: true,
                    Data: remoteResult,
                    Source: DataSource.Remote,
                    IsPending: false,
                    Error: null);
            }
            catch
            {
                // Remote failed - fall back to local, queue for later
                if (operation.CanQueueForSync)
                {
                    await QueueActionAsync(new QueuedAction
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        DataKey = operation.DataKey,
                        OperationType = operation.OperationType,
                        Payload = JsonSerializer.Serialize(localResult),
                        QueuedAt = DateTimeOffset.UtcNow,
                        Priority = operation.Priority
                    });
                }

                UpdateDataLocation(operation.DataKey, DataLocationState.LocalOnly);
            }
        }
        else if (operation.CanQueueForSync)
        {
            // Offline - queue for later sync
            await QueueActionAsync(new QueuedAction
            {
                Id = Guid.NewGuid().ToString("N"),
                DataKey = operation.DataKey,
                OperationType = operation.OperationType,
                Payload = JsonSerializer.Serialize(localResult),
                QueuedAt = DateTimeOffset.UtcNow,
                Priority = operation.Priority
            });

            UpdateDataLocation(operation.DataKey, DataLocationState.LocalPendingSync);
        }

        return new OfflineOperationResult<T>(
            Success: true,
            Data: localResult,
            Source: DataSource.Local,
            IsPending: !IsOnline && operation.CanQueueForSync,
            Error: null);
    }

    /// <summary>
    /// Gets data with offline fallback.
    /// </summary>
    public async Task<OfflineDataResult<T>> GetDataAsync<T>(
        string dataKey,
        Func<CancellationToken, Task<T>>? remoteLoader = null,
        CancellationToken cancellationToken = default) where T : class
    {
        // Try local first (always available)
        var localData = await _storage.GetLocalDataAsync<T>(dataKey, cancellationToken);
        var localTimestamp = await _storage.GetLocalTimestampAsync(dataKey, cancellationToken);

        if (IsOnline && remoteLoader != null)
        {
            try
            {
                var remoteData = await remoteLoader(cancellationToken);
                await _storage.SaveLocalDataAsync(dataKey, remoteData, cancellationToken);
                UpdateDataLocation(dataKey, DataLocationState.Synced);

                return new OfflineDataResult<T>(
                    Data: remoteData,
                    Source: DataSource.Remote,
                    Timestamp: DateTimeOffset.UtcNow,
                    IsStale: false);
            }
            catch
            {
                // Remote failed - return local if available
                if (localData != null)
                {
                    UpdateDataLocation(dataKey, DataLocationState.LocalStale);
                    return new OfflineDataResult<T>(
                        Data: localData,
                        Source: DataSource.LocalCache,
                        Timestamp: localTimestamp,
                        IsStale: true);
                }
                throw;
            }
        }

        // Offline - return local
        if (localData != null)
        {
            UpdateDataLocation(dataKey, DataLocationState.LocalOnly);
            return new OfflineDataResult<T>(
                Data: localData,
                Source: DataSource.LocalCache,
                Timestamp: localTimestamp,
                IsStale: !IsOnline);
        }

        // No data available
        return new OfflineDataResult<T>(
            Data: default,
            Source: DataSource.None,
            Timestamp: null,
            IsStale: false);
    }

    /// <summary>
    /// Queues an action for later sync.
    /// </summary>
    public async Task<string> QueueActionAsync(
        string operationType,
        object payload,
        SyncPriority priority = SyncPriority.Normal,
        CancellationToken cancellationToken = default)
    {
        var action = new QueuedAction
        {
            Id = Guid.NewGuid().ToString("N"),
            DataKey = $"{operationType}_{DateTimeOffset.UtcNow.Ticks}",
            OperationType = operationType,
            Payload = JsonSerializer.Serialize(payload),
            QueuedAt = DateTimeOffset.UtcNow,
            Priority = priority
        };

        await QueueActionAsync(action);
        return action.Id;
    }

    /// <summary>
    /// Gets the current action queue status.
    /// </summary>
    public QueueStatus GetQueueStatus()
    {
        var pending = _actionQueue.ToArray();
        var grouped = pending.GroupBy(a => a.OperationType)
            .ToDictionary(g => g.Key, g => g.Count());

        return new QueueStatus(
            TotalPending: pending.Length,
            OldestAction: pending.MinBy(a => a.QueuedAt)?.QueuedAt,
            ByOperationType: grouped,
            EstimatedSyncTime: EstimateSyncTime(pending.Length));
    }

    // ========================================================================
    // RECONNECT: Automatic Sync Resume
    // ========================================================================

    /// <summary>
    /// Starts the sync process for all pending actions.
    /// </summary>
    public async Task<SyncResult> SyncAsync(
        SyncOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (_isSyncing)
        {
            return new SyncResult(
                Success: false,
                SyncedCount: 0,
                FailedCount: 0,
                ConflictCount: 0,
                Errors: ["Sync already in progress"]);
        }

        if (!IsOnline)
        {
            return new SyncResult(
                Success: false,
                SyncedCount: 0,
                FailedCount: 0,
                ConflictCount: 0,
                Errors: ["Cannot sync while offline"]);
        }

        _isSyncing = true;
        await UpdateStateAsync(ConnectivityState.Syncing);

        try
        {
            var opts = options ?? SyncOptions.Default;
            var synced = 0;
            var failed = 0;
            var conflicts = new List<SyncConflict>();
            var errors = new List<string>();

            // Process queue by priority
            var pendingActions = _actionQueue.ToArray()
                .OrderByDescending(a => a.Priority)
                .ThenBy(a => a.QueuedAt)
                .ToList();

            var total = pendingActions.Count;
            var processed = 0;

            foreach (var action in pendingActions)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var result = await _syncEngine.SyncActionAsync(action, cancellationToken);

                    if (result.HasConflict)
                    {
                        conflicts.Add(result.Conflict!);
                        OnConflictDetected(result.Conflict!);

                        if (!opts.AutoResolveConflicts)
                        {
                            continue; // Leave in queue for manual resolution
                        }

                        // Auto-resolve based on strategy
                        var resolved = await ResolveConflictAsync(
                            result.Conflict!, opts.ConflictStrategy, cancellationToken);

                        if (!resolved)
                        {
                            continue;
                        }
                    }

                    // Remove from queue on success
                    RemoveFromQueue(action.Id);
                    synced++;

                    // Update data location
                    UpdateDataLocation(action.DataKey, DataLocationState.Synced);
                }
                catch (Exception ex)
                {
                    action.RetryCount++;
                    action.LastError = ex.Message;

                    if (action.RetryCount >= opts.MaxRetries)
                    {
                        errors.Add($"Failed to sync {action.OperationType}: {ex.Message}");
                        RemoveFromQueue(action.Id);
                        failed++;
                    }
                }

                processed++;
                OnSyncProgress(processed, total, synced, failed, conflicts.Count);
            }

            return new SyncResult(
                Success: failed == 0 && conflicts.Count == 0,
                SyncedCount: synced,
                FailedCount: failed,
                ConflictCount: conflicts.Count,
                Conflicts: conflicts,
                Errors: errors);
        }
        finally
        {
            _isSyncing = false;
            await UpdateStateAsync(IsOnline ? ConnectivityState.Online : ConnectivityState.Offline);
        }
    }

    /// <summary>
    /// Gets all current sync conflicts.
    /// </summary>
    public async Task<IReadOnlyList<SyncConflict>> GetConflictsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _storage.GetConflictsAsync(cancellationToken);
    }

    /// <summary>
    /// Resolves a sync conflict with the specified strategy.
    /// </summary>
    public async Task<bool> ResolveConflictAsync(
        string conflictId,
        ConflictResolution resolution,
        CancellationToken cancellationToken = default)
    {
        var conflict = await _storage.GetConflictAsync(conflictId, cancellationToken);
        if (conflict == null)
            return false;

        return await ResolveConflictAsync(conflict, resolution, cancellationToken);
    }

    // ========================================================================
    // TRUST: Data Location Transparency
    // ========================================================================

    /// <summary>
    /// Gets where data currently lives.
    /// </summary>
    public DataLocation GetDataLocation(string dataKey)
    {
        return _dataLocations.TryGetValue(dataKey, out var location)
            ? location
            : new DataLocation(dataKey, DataLocationState.Unknown, null, null);
    }

    /// <summary>
    /// Gets all data locations with their sync status.
    /// </summary>
    public IReadOnlyDictionary<string, DataLocation> GetAllDataLocations()
    {
        return _dataLocations.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Gets a user-friendly summary of data status.
    /// </summary>
    public DataStatusSummary GetDataStatusSummary()
    {
        var locations = _dataLocations.Values.ToList();

        var synced = locations.Count(l => l.State == DataLocationState.Synced);
        var pendingSync = locations.Count(l => l.State == DataLocationState.LocalPendingSync);
        var localOnly = locations.Count(l => l.State == DataLocationState.LocalOnly);
        var stale = locations.Count(l => l.State == DataLocationState.LocalStale);

        var message = (synced, pendingSync, localOnly, stale) switch
        {
            (_, 0, 0, 0) => "All data synced",
            (_, > 0, _, _) => $"{pendingSync} changes waiting to sync",
            (_, 0, > 0, _) => $"{localOnly} items stored locally",
            (_, 0, 0, > 0) => $"{stale} items may be outdated",
            _ => "Data status mixed"
        };

        return new DataStatusSummary(
            TotalItems: locations.Count,
            SyncedCount: synced,
            PendingSyncCount: pendingSync,
            LocalOnlyCount: localOnly,
            StaleCount: stale,
            Message: message,
            Icon: pendingSync > 0 ? "\uE895" : "\uE73E");
    }

    // ========================================================================
    // Private Helpers
    // ========================================================================

    private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        var previousState = _currentState;
        await UpdateStateAsync(e.NewState);

        // Auto-sync on reconnect
        if (previousState == ConnectivityState.Offline &&
            e.NewState == ConnectivityState.Online &&
            _actionQueue.Count > 0)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2)); // Brief delay for stability
                if (IsOnline)
                {
                    await SyncAsync();
                }
            });
        }
    }

    private async Task UpdateStateAsync(ConnectivityState newState)
    {
        var previousState = _currentState;
        _currentState = newState;

        if (newState == ConnectivityState.Offline && previousState != ConnectivityState.Offline)
        {
            _offlineSince = DateTimeOffset.UtcNow;
        }
        else if (newState == ConnectivityState.Online)
        {
            _offlineSince = null;
        }

        if (previousState != newState)
        {
            ConnectivityChanged?.Invoke(this, new ConnectivityChangedEventArgs(
                previousState, newState, DateTimeOffset.UtcNow));
        }

        await Task.CompletedTask;
    }

    private string GetOfflineMessage()
    {
        var duration = OfflineDuration;
        if (!duration.HasValue)
            return "Offline";

        return duration.Value.TotalMinutes switch
        {
            < 1 => "Offline (just now)",
            < 60 => $"Offline ({duration.Value.Minutes}m)",
            < 1440 => $"Offline ({duration.Value.Hours}h {duration.Value.Minutes}m)",
            _ => $"Offline ({duration.Value.Days}d)"
        };
    }

    private IReadOnlyList<HealthCheckEndpoint> GetHealthCheckEndpoints()
    {
        return
        [
            new HealthCheckEndpoint("Primary API", "https://api.controlroom.local/health", 5000),
            new HealthCheckEndpoint("DNS Check", "https://dns.google/resolve?name=controlroom.local", 3000),
            new HealthCheckEndpoint("Fallback", "https://1.1.1.1/cdn-cgi/trace", 3000)
        ];
    }

    private async Task<EndpointCheckResult> CheckEndpointAsync(
        HealthCheckEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(endpoint.TimeoutMs);

            var startTime = DateTimeOffset.UtcNow;
            var isReachable = await _connectivityMonitor.CheckEndpointAsync(endpoint.Url, cts.Token);
            var latency = DateTimeOffset.UtcNow - startTime;

            return new EndpointCheckResult(
                Name: endpoint.Name,
                Url: endpoint.Url,
                IsReachable: isReachable,
                Latency: latency,
                Error: null);
        }
        catch (Exception ex)
        {
            return new EndpointCheckResult(
                Name: endpoint.Name,
                Url: endpoint.Url,
                IsReachable: false,
                Latency: null,
                Error: ex.Message);
        }
    }

    private async Task QueueActionAsync(QueuedAction action)
    {
        _actionQueue.Enqueue(action);
        await _storage.SaveQueueAsync(_actionQueue.ToList());
    }

    private void RemoveFromQueue(string actionId)
    {
        var items = _actionQueue.ToArray().Where(a => a.Id != actionId).ToList();
        while (_actionQueue.TryDequeue(out _)) { } // Clear queue
        foreach (var item in items)
        {
            _actionQueue.Enqueue(item);
        }
    }

    private void UpdateDataLocation(string dataKey, DataLocationState state)
    {
        var location = new DataLocation(
            DataKey: dataKey,
            State: state,
            LocalTimestamp: DateTimeOffset.UtcNow,
            RemoteTimestamp: state == DataLocationState.Synced ? DateTimeOffset.UtcNow : null);

        var previousLocation = _dataLocations.GetValueOrDefault(dataKey);
        _dataLocations[dataKey] = location;

        if (previousLocation?.State != state)
        {
            DataLocationChanged?.Invoke(this, new DataLocationChangedEventArgs(
                dataKey, previousLocation?.State, state));
        }
    }

    private async Task<bool> ResolveConflictAsync(
        SyncConflict conflict,
        ConflictResolution resolution,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (resolution)
            {
                case ConflictResolution.KeepLocal:
                    await _syncEngine.PushLocalAsync(conflict.DataKey, cancellationToken);
                    break;

                case ConflictResolution.KeepRemote:
                    await _syncEngine.PullRemoteAsync(conflict.DataKey, cancellationToken);
                    break;

                case ConflictResolution.KeepBoth:
                    await _syncEngine.MergeBothAsync(conflict.DataKey, cancellationToken);
                    break;

                case ConflictResolution.KeepNewer:
                    if (conflict.LocalTimestamp > conflict.RemoteTimestamp)
                        await _syncEngine.PushLocalAsync(conflict.DataKey, cancellationToken);
                    else
                        await _syncEngine.PullRemoteAsync(conflict.DataKey, cancellationToken);
                    break;

                default:
                    return false;
            }

            await _storage.RemoveConflictAsync(conflict.Id, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static TimeSpan? EstimateSyncTime(int pendingCount)
    {
        if (pendingCount == 0) return null;
        // Rough estimate: 500ms per action
        return TimeSpan.FromMilliseconds(pendingCount * 500);
    }

    private void OnSyncProgress(int processed, int total, int synced, int failed, int conflicts)
    {
        SyncProgress?.Invoke(this, new SyncProgressEventArgs(
            processed, total, synced, failed, conflicts));
    }

    private void OnConflictDetected(SyncConflict conflict)
    {
        ConflictDetected?.Invoke(this, new ConflictDetectedEventArgs(conflict));
    }
}

// ============================================================================
// Offline-First Types
// ============================================================================

/// <summary>
/// Connectivity state.
/// </summary>
public enum ConnectivityState
{
    Unknown,
    Online,
    Offline,
    LimitedConnectivity,
    Syncing
}

/// <summary>
/// Status color for UI.
/// </summary>
public enum StatusColor
{
    Green,
    Yellow,
    Blue,
    Gray,
    Red
}

/// <summary>
/// User-friendly connectivity status.
/// </summary>
public sealed record ConnectivityStatus(
    ConnectivityState State,
    string Icon,
    string Message,
    StatusColor Color,
    DateTimeOffset? OfflineSince,
    int PendingActionsCount,
    bool CanWorkOffline);

/// <summary>
/// Connectivity check result.
/// </summary>
public sealed record ConnectivityCheckResult(
    bool IsOnline,
    ConnectivityState State,
    TimeSpan? Latency,
    IReadOnlyList<EndpointCheckResult> EndpointResults,
    DateTimeOffset CheckedAt,
    string? Error = null);

/// <summary>
/// Endpoint check result.
/// </summary>
public sealed record EndpointCheckResult(
    string Name,
    string Url,
    bool IsReachable,
    TimeSpan? Latency,
    string? Error);

/// <summary>
/// Health check endpoint configuration.
/// </summary>
public sealed record HealthCheckEndpoint(
    string Name,
    string Url,
    int TimeoutMs);

/// <summary>
/// Offline operation definition.
/// </summary>
public sealed record OfflineOperation<T>(
    string DataKey,
    string OperationType,
    Func<CancellationToken, Task<T>> LocalExecute,
    Func<CancellationToken, Task<T>>? RemoteExecute = null,
    bool CanQueueForSync = true,
    SyncPriority Priority = SyncPriority.Normal);

/// <summary>
/// Result of an offline operation.
/// </summary>
public sealed record OfflineOperationResult<T>(
    bool Success,
    T? Data,
    DataSource Source,
    bool IsPending,
    string? Error);

/// <summary>
/// Result of getting offline data.
/// </summary>
public sealed record OfflineDataResult<T>(
    T? Data,
    DataSource Source,
    DateTimeOffset? Timestamp,
    bool IsStale);

/// <summary>
/// Data source.
/// </summary>
public enum DataSource
{
    None,
    Local,
    LocalCache,
    Remote
}

/// <summary>
/// Sync priority.
/// </summary>
public enum SyncPriority
{
    Low,
    Normal,
    High,
    Critical
}

/// <summary>
/// Queued action for sync.
/// </summary>
public sealed class QueuedAction
{
    public required string Id { get; set; }
    public required string DataKey { get; set; }
    public required string OperationType { get; set; }
    public required string Payload { get; set; }
    public required DateTimeOffset QueuedAt { get; set; }
    public SyncPriority Priority { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}

/// <summary>
/// Queue status.
/// </summary>
public sealed record QueueStatus(
    int TotalPending,
    DateTimeOffset? OldestAction,
    IReadOnlyDictionary<string, int> ByOperationType,
    TimeSpan? EstimatedSyncTime);

/// <summary>
/// Sync options.
/// </summary>
public sealed record SyncOptions(
    bool AutoResolveConflicts = false,
    ConflictResolution ConflictStrategy = ConflictResolution.KeepNewer,
    int MaxRetries = 3,
    TimeSpan? Timeout = null)
{
    public static SyncOptions Default => new();
}

/// <summary>
/// Sync result.
/// </summary>
public sealed record SyncResult(
    bool Success,
    int SyncedCount,
    int FailedCount,
    int ConflictCount,
    IReadOnlyList<SyncConflict>? Conflicts = null,
    IReadOnlyList<string>? Errors = null);

/// <summary>
/// Sync conflict.
/// </summary>
public sealed record SyncConflict(
    string Id,
    string DataKey,
    string LocalValue,
    string RemoteValue,
    DateTimeOffset LocalTimestamp,
    DateTimeOffset RemoteTimestamp,
    string Description);

/// <summary>
/// Conflict resolution strategy.
/// </summary>
public enum ConflictResolution
{
    Manual,
    KeepLocal,
    KeepRemote,
    KeepBoth,
    KeepNewer
}

/// <summary>
/// Data location state.
/// </summary>
public enum DataLocationState
{
    Unknown,
    Synced,
    LocalOnly,
    LocalPendingSync,
    LocalStale,
    RemoteOnly,
    Local
}

/// <summary>
/// Where data lives.
/// </summary>
public sealed record DataLocation(
    string DataKey,
    DataLocationState State,
    DateTimeOffset? LocalTimestamp,
    DateTimeOffset? RemoteTimestamp);

/// <summary>
/// Data status summary.
/// </summary>
public sealed record DataStatusSummary(
    int TotalItems,
    int SyncedCount,
    int PendingSyncCount,
    int LocalOnlyCount,
    int StaleCount,
    string Message,
    string Icon);

// ============================================================================
// Events
// ============================================================================

/// <summary>
/// Connectivity changed event args.
/// </summary>
public sealed class ConnectivityChangedEventArgs : EventArgs
{
    public ConnectivityState PreviousState { get; }
    public ConnectivityState NewState { get; }
    public DateTimeOffset Timestamp { get; }

    public ConnectivityChangedEventArgs(
        ConnectivityState previousState,
        ConnectivityState newState,
        DateTimeOffset timestamp)
    {
        PreviousState = previousState;
        NewState = newState;
        Timestamp = timestamp;
    }
}

/// <summary>
/// Sync progress event args.
/// </summary>
public sealed class SyncProgressEventArgs : EventArgs
{
    public int Processed { get; }
    public int Total { get; }
    public int Synced { get; }
    public int Failed { get; }
    public int Conflicts { get; }
    public double Progress => Total > 0 ? (double)Processed / Total * 100 : 0;

    public SyncProgressEventArgs(int processed, int total, int synced, int failed, int conflicts)
    {
        Processed = processed;
        Total = total;
        Synced = synced;
        Failed = failed;
        Conflicts = conflicts;
    }
}

/// <summary>
/// Conflict detected event args.
/// </summary>
public sealed class ConflictDetectedEventArgs : EventArgs
{
    public SyncConflict Conflict { get; }

    public ConflictDetectedEventArgs(SyncConflict conflict)
    {
        Conflict = conflict;
    }
}

/// <summary>
/// Data location changed event args.
/// </summary>
public sealed class DataLocationChangedEventArgs : EventArgs
{
    public string DataKey { get; }
    public DataLocationState? PreviousState { get; }
    public DataLocationState NewState { get; }

    public DataLocationChangedEventArgs(
        string dataKey,
        DataLocationState? previousState,
        DataLocationState newState)
    {
        DataKey = dataKey;
        PreviousState = previousState;
        NewState = newState;
    }
}

// ============================================================================
// Interfaces
// ============================================================================

/// <summary>
/// Offline storage repository.
/// </summary>
public interface IOfflineStorageRepository
{
    Task SaveLocalDataAsync<T>(string key, T data, CancellationToken cancellationToken);
    Task<T?> GetLocalDataAsync<T>(string key, CancellationToken cancellationToken) where T : class;
    Task<DateTimeOffset?> GetLocalTimestampAsync(string key, CancellationToken cancellationToken);
    Task SaveQueueAsync(List<QueuedAction> queue, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SyncConflict>> GetConflictsAsync(CancellationToken cancellationToken);
    Task<SyncConflict?> GetConflictAsync(string conflictId, CancellationToken cancellationToken);
    Task RemoveConflictAsync(string conflictId, CancellationToken cancellationToken);
}

/// <summary>
/// Connectivity monitor.
/// </summary>
public interface IConnectivityMonitor
{
    event EventHandler<ConnectivityChangedEventArgs>? ConnectivityChanged;
    Task<bool> CheckEndpointAsync(string url, CancellationToken cancellationToken);
}

/// <summary>
/// Sync engine.
/// </summary>
public interface ISyncEngine
{
    Task<SyncActionResult> SyncActionAsync(QueuedAction action, CancellationToken cancellationToken);
    Task PushLocalAsync(string dataKey, CancellationToken cancellationToken);
    Task PullRemoteAsync(string dataKey, CancellationToken cancellationToken);
    Task MergeBothAsync(string dataKey, CancellationToken cancellationToken);
}

/// <summary>
/// Result of syncing an action.
/// </summary>
public sealed record SyncActionResult(
    bool Success,
    bool HasConflict,
    SyncConflict? Conflict);
