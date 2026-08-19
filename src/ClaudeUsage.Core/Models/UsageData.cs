namespace ClaudeUsage.Core.Models;

public sealed class ModelUsage
{
    public required string ModelId { get; init; }
    public required string DisplayName { get; init; }
    public long TotalTokens { get; init; }

    /// <summary>Share of today's total tokens attributable to this model, 0-100.</summary>
    public double SharePercent { get; init; }
}

public sealed class UsageData
{
    public DateTimeOffset Timestamp { get; init; }
    public int Requests { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public long TotalTokens => InputTokens + OutputTokens;
    public decimal EstimatedCostUsd { get; init; }

    /// <summary>Percentage of the user-configured daily limit consumed so far, 0-100 (may exceed 100).</summary>
    public double LimitUsagePercent { get; init; }

    public IReadOnlyList<ModelUsage> ModelBreakdown { get; init; } = Array.Empty<ModelUsage>();
}

public sealed class CostData
{
    public decimal Today { get; init; }
    public decimal MonthToDate { get; init; }
    public decimal ProjectedMonth { get; init; }

    /// <summary>Signed percent change vs. the previous comparable period, e.g. +12.4 or -3.1.</summary>
    public double PercentChangeFromPreviousPeriod { get; init; }
}

public sealed class UsageHistoryPoint
{
    public DateTimeOffset Timestamp { get; init; }
    public double UsagePercent { get; init; }
    public long TotalTokens { get; init; }
}

/// <summary>The full result of one usage fetch, as returned by an <see cref="Services.IUsageProvider"/>.</summary>
public sealed record UsageSnapshot
{
    public required UsageData Today { get; init; }
    public required CostData Cost { get; init; }
    public IReadOnlyList<UsageHistoryPoint> History { get; init; } = Array.Empty<UsageHistoryPoint>();
    public ApiConnectionState ConnectionState { get; init; }
    public DateTimeOffset RetrievedAt { get; init; }
    public bool IsFromCache { get; init; }
    public string? ErrorMessage { get; init; }
}
