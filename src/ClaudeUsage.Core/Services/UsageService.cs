using System.Threading;
using ClaudeUsage.Core.Models;
using Microsoft.Extensions.Logging;

namespace ClaudeUsage.Core.Services;

/// <summary>
/// Orchestrates <see cref="IUsageProvider"/> calls: picks demo vs. live, debounces manual refreshes,
/// backs off after repeated failures, persists a cache for offline display, and raises threshold
/// notifications. This is the only class the UI/ViewModel layer talks to for usage data.
/// </summary>
public sealed class UsageService : IUsageService
{
    private static readonly TimeSpan ManualRefreshDebounce = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(10);

    private readonly IUsageProvider _demoProvider;
    private readonly ClaudeUsageProvider _liveProvider;
    private readonly IStorageService _storage;
    private readonly INotificationService? _notifications;
    private readonly ILogger<UsageService> _logger;

    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private CancellationTokenSource? _autoRefreshCts;

    private AppSettings _settings = new();
    private DateTimeOffset _lastRefreshAttempt = DateTimeOffset.MinValue;
    private int _consecutiveFailures;
    private DateTimeOffset _backoffUntil = DateTimeOffset.MinValue;
    private readonly HashSet<int> _notifiedThresholdsToday = [];
    private DateOnly _notifiedThresholdsDate = DateOnly.MinValue;

    public UsageSnapshot? Current { get; private set; }
    public ApiConnectionState ConnectionState { get; private set; } = ApiConnectionState.Offline;
    public UsagePeriod HistoryPeriod { get; set; } = UsagePeriod.Last24Hours;

    public event EventHandler<UsageSnapshot>? UsageUpdated;
    public event EventHandler<ApiConnectionState>? ConnectionStateChanged;
    public event EventHandler<int>? UsageThresholdCrossed;

    public UsageService(
        DemoUsageProvider demoProvider,
        ClaudeUsageProvider liveProvider,
        IStorageService storage,
        INotificationService? notifications = null,
        ILogger<UsageService>? logger = null)
    {
        _demoProvider = demoProvider;
        _liveProvider = liveProvider;
        _storage = storage;
        _notifications = notifications;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<UsageService>.Instance;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _settings = await _storage.LoadSettingsAsync(cancellationToken).ConfigureAwait(false);
        ApplyEndpointToLiveProvider();

        var cached = await _storage.LoadCachedSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            Current = cached with { IsFromCache = true };
            SetConnectionState(ApiConnectionState.Offline);
            UsageUpdated?.Invoke(this, Current);
            _logger.LogInformation("Loaded cached usage snapshot from {RetrievedAt}.", cached.RetrievedAt);
        }

        RestartAutoRefresh();
        await RefreshAsync(userInitiated: false, cancellationToken).ConfigureAwait(false);
    }

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        ApplyEndpointToLiveProvider();
        RestartAutoRefresh();
    }

    public async Task RefreshAsync(bool userInitiated = false, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.Now;

        if (userInitiated && now - _lastRefreshAttempt < ManualRefreshDebounce)
        {
            _logger.LogDebug("Manual refresh ignored (debounced).");
            return;
        }

        if (!userInitiated && now < _backoffUntil)
        {
            _logger.LogDebug("Scheduled refresh skipped (backing off until {BackoffUntil}).", _backoffUntil);
            return;
        }

        if (!await _refreshGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogDebug("Refresh already in progress; skipping.");
            return;
        }

        _lastRefreshAttempt = now;
        try
        {
            SetConnectionState(ApiConnectionState.Connecting);
            var provider = _settings.DemoModeEnabled ? _demoProvider : (IUsageProvider)_liveProvider;

            var snapshot = await provider.GetUsageAsync(HistoryPeriod, cancellationToken).ConfigureAwait(false);

            _consecutiveFailures = 0;
            _backoffUntil = DateTimeOffset.MinValue;

            Current = snapshot;
            SetConnectionState(snapshot.ConnectionState);
            UsageUpdated?.Invoke(this, snapshot);

            await _storage.SaveCachedSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
            CheckThresholds(snapshot.Today.LimitUsagePercent);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Includes UsageProviderException subtypes and any unexpected provider bug — an API/provider
            // failure must never crash the app, so everything else collapses to a generic Error state.
            HandleFailure(ex);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private void HandleFailure(Exception ex)
    {
        _consecutiveFailures++;
        var backoffSeconds = Math.Min(MinBackoff.TotalSeconds * Math.Pow(2, _consecutiveFailures - 1), MaxBackoff.TotalSeconds);
        if (ex is ApiRateLimitedException { RetryAfter: { } retryAfter })
        {
            backoffSeconds = Math.Max(backoffSeconds, retryAfter.TotalSeconds);
        }

        _backoffUntil = DateTimeOffset.Now.AddSeconds(backoffSeconds);

        var state = ex switch
        {
            ApiUnauthorizedException => ApiConnectionState.Unauthorized,
            ApiRateLimitedException => ApiConnectionState.RateLimited,
            ApiUnavailableException or InvalidUsageResponseException => ApiConnectionState.Error,
            _ => ApiConnectionState.Error,
        };

        _logger.LogWarning(ex, "Usage refresh failed ({State}); next attempt in {Backoff}s.", state, backoffSeconds);
        SetConnectionState(state);

        if (Current is not null)
        {
            Current = Current with { ConnectionState = state, IsFromCache = true, ErrorMessage = ex.Message };
        }
    }

    private void SetConnectionState(ApiConnectionState state)
    {
        if (ConnectionState == state)
        {
            return;
        }

        var wasDown = ConnectionState is ApiConnectionState.Error or ApiConnectionState.Offline or ApiConnectionState.Unauthorized or ApiConnectionState.RateLimited;
        ConnectionState = state;
        ConnectionStateChanged?.Invoke(this, state);

        if (wasDown && state == ApiConnectionState.Connected)
        {
            _notifications?.ShowConnectionRestored();
        }
    }

    private void CheckThresholds(double usagePercent)
    {
        if (!_settings.NotificationsEnabled)
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        if (today != _notifiedThresholdsDate)
        {
            _notifiedThresholdsDate = today;
            _notifiedThresholdsToday.Clear();
        }

        foreach (var threshold in _settings.NotificationThresholds.OrderBy(t => t))
        {
            if (usagePercent >= threshold && _notifiedThresholdsToday.Add(threshold))
            {
                UsageThresholdCrossed?.Invoke(this, threshold);
                _notifications?.ShowUsageThresholdAlert(threshold);
            }
        }
    }

    private void ApplyEndpointToLiveProvider()
    {
        _liveProvider.Endpoint = string.IsNullOrWhiteSpace(_settings.ApiEndpoint)
            ? null
            : Uri.TryCreate(_settings.ApiEndpoint, UriKind.Absolute, out var uri) ? uri : null;
    }

    private void RestartAutoRefresh()
    {
        _autoRefreshCts?.Cancel();
        _autoRefreshCts?.Dispose();

        var interval = _settings.RefreshInterval.ToTimeSpan();
        if (interval is null)
        {
            _autoRefreshCts = null;
            return;
        }

        var cts = new CancellationTokenSource();
        _autoRefreshCts = cts;
        _ = AutoRefreshLoopAsync(interval.Value, cts.Token);
    }

    private async Task AutoRefreshLoopAsync(TimeSpan interval, CancellationToken token)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                await RefreshAsync(userInitiated: false, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Interval changed or service disposed — expected.
        }
    }

    public void Dispose()
    {
        _autoRefreshCts?.Cancel();
        _autoRefreshCts?.Dispose();
        _refreshGate.Dispose();
    }
}
