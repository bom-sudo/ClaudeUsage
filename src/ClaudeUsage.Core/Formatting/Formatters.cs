using System.Globalization;

namespace ClaudeUsage.Core.Formatting;

/// <summary>Pure display-formatting helpers shared by the dashboard and tray tooltip. No UI dependencies, so it's unit-testable on its own.</summary>
public static class Formatters
{
    public static string FormatTokenCount(long tokens) => tokens switch
    {
        >= 1_000_000_000 => $"{tokens / 1_000_000_000.0:0.##}B",
        >= 1_000_000 => $"{tokens / 1_000_000.0:0.##}M",
        >= 1_000 => $"{tokens / 1_000.0:0.##}K",
        _ => tokens.ToString(CultureInfo.InvariantCulture),
    };

    public static string FormatRequestCount(int requests) => requests.ToString("N0", CultureInfo.InvariantCulture);

    public static string FormatCost(decimal amountUsd) => amountUsd.ToString("C2", CultureInfo.GetCultureInfo("en-US"));

    public static string FormatPercent(double percent) => $"{Math.Round(percent):0}%";

    public static string FormatSignedPercent(double percent)
    {
        var arrow = percent >= 0 ? "↑" : "↓";
        return $"{arrow} {Math.Abs(percent):0.#}%";
    }

    public static string FormatRelativeTime(DateTimeOffset timestamp, DateTimeOffset? now = null)
    {
        var reference = now ?? DateTimeOffset.Now;
        var elapsed = reference - timestamp;

        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        return elapsed switch
        {
            { TotalSeconds: < 5 } => "just now",
            { TotalSeconds: < 60 } => $"{(int)elapsed.TotalSeconds} seconds ago",
            { TotalMinutes: < 2 } => "1 minute ago",
            { TotalMinutes: < 60 } => $"{(int)elapsed.TotalMinutes} minutes ago",
            { TotalHours: < 2 } => "1 hour ago",
            { TotalHours: < 24 } => $"{(int)elapsed.TotalHours} hours ago",
            _ => timestamp.LocalDateTime.ToString("MMM d, HH:mm", CultureInfo.InvariantCulture),
        };
    }

    public static string FormatClockTime(DateTimeOffset timestamp) => timestamp.LocalDateTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
}
