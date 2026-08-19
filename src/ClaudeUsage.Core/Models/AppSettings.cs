namespace ClaudeUsage.Core.Models;

/// <summary>
/// User-configurable preferences. Persisted as JSON via <see cref="Services.IStorageService"/>.
/// Never put secrets here — the API key lives in platform secure storage via <see cref="Services.ISecretProvider"/>.
/// </summary>
public sealed class AppSettings
{
    public string ApiEndpoint { get; set; } = string.Empty;
    public bool DemoModeEnabled { get; set; } = true;

    public RefreshInterval RefreshInterval { get; set; } = RefreshInterval.Minutes5;
    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>0.0 (opaque) - 1.0 (fully transparent Acrylic/Mica tint).</summary>
    public double TransparencyLevel { get; set; } = 0.8;
    public bool AnimationsEnabled { get; set; } = true;
    public bool CompactMode { get; set; } = false;

    public bool StartWithWindows { get; set; } = false;

    public bool NotificationsEnabled { get; set; } = true;
    public List<int> NotificationThresholds { get; set; } = new() { 80, 95 };

    /// <summary>Daily token budget used to compute the usage-limit percentage.</summary>
    public double DailyTokenLimit { get; set; } = 5_000_000;

    public bool VerboseLoggingEnabled { get; set; } = false;
}
