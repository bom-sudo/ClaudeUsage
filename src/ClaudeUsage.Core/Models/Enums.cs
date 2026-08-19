namespace ClaudeUsage.Core.Models;

public enum ApiConnectionState
{
    Connecting,
    Connected,
    Error,
    Unauthorized,
    RateLimited,
    Offline,
}

public enum UsagePeriod
{
    Last24Hours,
    Last7Days,
    Last30Days,
}

public enum RefreshInterval
{
    Seconds30,
    Minute1,
    Minutes5,
    Minutes15,
    Disabled,
}

public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>Usage severity band used to color the limit indicator. Thresholds match the product spec (0-50/51-75/76-90/91-100).</summary>
public enum UsageState
{
    Normal,
    Moderate,
    Warning,
    Critical,
}

public static class RefreshIntervalExtensions
{
    public static TimeSpan? ToTimeSpan(this RefreshInterval interval) => interval switch
    {
        RefreshInterval.Seconds30 => TimeSpan.FromSeconds(30),
        RefreshInterval.Minute1 => TimeSpan.FromMinutes(1),
        RefreshInterval.Minutes5 => TimeSpan.FromMinutes(5),
        RefreshInterval.Minutes15 => TimeSpan.FromMinutes(15),
        RefreshInterval.Disabled => null,
        _ => throw new ArgumentOutOfRangeException(nameof(interval)),
    };
}

public static class UsageStateCalculator
{
    public static UsageState FromPercent(double percent) => percent switch
    {
        <= 50 => UsageState.Normal,
        <= 75 => UsageState.Moderate,
        <= 90 => UsageState.Warning,
        _ => UsageState.Critical,
    };
}
