using System.Threading;
using ClaudeUsage.Core.Models;

namespace ClaudeUsage.Core.Services;

/// <summary>
/// Fully self-contained fake data source for UI development/testing. Makes zero network calls.
/// Values are seeded from the calendar day so a session's numbers stay stable across refreshes
/// but still differ day to day, with a small jitter so counters visibly animate on refresh.
/// </summary>
public sealed class DemoUsageProvider : IUsageProvider
{
    public string Name => "Demo Mode";

    private static readonly (string Id, string DisplayName, double BaseShare)[] Models =
    [
        ("claude-opus-4", "Claude Opus", 0.48),
        ("claude-sonnet-5", "Claude Sonnet", 0.35),
        ("claude-haiku-4.5", "Claude Haiku", 0.17),
    ];

    public Task<UsageSnapshot> GetUsageAsync(UsagePeriod historyPeriod, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.Now;
        var daySeed = now.Date.DayOfYear + now.Year * 1000;
        var rng = new Random(daySeed);
        var jitter = new Random(HashCode.Combine(daySeed, now.Minute / 5));

        var baseRequests = 900 + rng.Next(0, 700);
        var requests = baseRequests + jitter.Next(-20, 20);
        var inputTokens = (long)(1_500_000 + rng.NextDouble() * 500_000);
        var outputTokens = (long)(950_000 + rng.NextDouble() * 400_000);
        var totalTokens = inputTokens + outputTokens;
        var cost = Math.Round((decimal)(totalTokens / 1_000_000.0 * 1.70), 2);
        var limitPercent = Math.Clamp(totalTokens / 5_000_000.0 * 100, 0, 140);

        var modelBreakdown = Models
            .Select(m =>
            {
                var share = Math.Clamp(m.BaseShare + (rng.NextDouble() - 0.5) * 0.04, 0.02, 0.9);
                return new ModelUsage
                {
                    ModelId = m.Id,
                    DisplayName = m.DisplayName,
                    TotalTokens = (long)(totalTokens * share),
                    SharePercent = Math.Round(share * 100, 1),
                };
            })
            .ToList();

        var today = new UsageData
        {
            Timestamp = now,
            Requests = requests,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            EstimatedCostUsd = cost,
            LimitUsagePercent = limitPercent,
            ModelBreakdown = modelBreakdown,
        };

        var monthToDate = cost * (decimal)(now.Day * 0.92);
        var projected = monthToDate / now.Day * DateTime.DaysInMonth(now.Year, now.Month);

        var costData = new CostData
        {
            Today = cost,
            MonthToDate = Math.Round(monthToDate, 2),
            ProjectedMonth = Math.Round(projected, 2),
            PercentChangeFromPreviousPeriod = Math.Round(8 + rng.NextDouble() * 10, 1),
        };

        var history = BuildHistory(historyPeriod, rng);

        var snapshot = new UsageSnapshot
        {
            Today = today,
            Cost = costData,
            History = history,
            ConnectionState = ApiConnectionState.Connected,
            RetrievedAt = now,
            IsFromCache = false,
        };

        return Task.FromResult(snapshot);
    }

    private static List<UsageHistoryPoint> BuildHistory(UsagePeriod period, Random rng)
    {
        var (points, step) = period switch
        {
            UsagePeriod.Last24Hours => (24, TimeSpan.FromHours(1)),
            UsagePeriod.Last7Days => (7, TimeSpan.FromDays(1)),
            UsagePeriod.Last30Days => (30, TimeSpan.FromDays(1)),
            _ => throw new ArgumentOutOfRangeException(nameof(period)),
        };

        var now = DateTimeOffset.Now;
        var result = new List<UsageHistoryPoint>(points);
        double level = 30 + rng.NextDouble() * 20;

        for (var i = points - 1; i >= 0; i--)
        {
            level = Math.Clamp(level + (rng.NextDouble() - 0.5) * 25, 5, 100);
            result.Add(new UsageHistoryPoint
            {
                Timestamp = now - step * i,
                UsagePercent = Math.Round(level, 1),
                TotalTokens = (long)(level / 100.0 * 3_000_000),
            });
        }

        return result;
    }
}
