using System.Threading;
using ClaudeUsage.Core.Models;

namespace ClaudeUsage.Core.Services;

/// <summary>
/// The single point of contact between the UI/ViewModels and usage data. Owns provider selection
/// (demo vs. live), caching, throttling, auto-refresh scheduling, and threshold notifications.
/// </summary>
public interface IUsageService : IDisposable
{
    UsageSnapshot? Current { get; }
    ApiConnectionState ConnectionState { get; }
    UsagePeriod HistoryPeriod { get; set; }

    event EventHandler<UsageSnapshot>? UsageUpdated;
    event EventHandler<ApiConnectionState>? ConnectionStateChanged;
    event EventHandler<int>? UsageThresholdCrossed;

    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <param name="userInitiated">True for an explicit Refresh click — bypasses the auto-refresh interval but is still debounced against rapid repeated clicks.</param>
    Task RefreshAsync(bool userInitiated = false, CancellationToken cancellationToken = default);

    void ApplySettings(AppSettings settings);
}
