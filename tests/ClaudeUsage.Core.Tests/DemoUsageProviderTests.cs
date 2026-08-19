using ClaudeUsage.Core.Models;
using ClaudeUsage.Core.Services;
using Xunit;

namespace ClaudeUsage.Core.Tests;

public class DemoUsageProviderTests
{
    [Fact]
    public async Task GetUsageAsync_ReturnsConnectedSnapshotWithTotals()
    {
        var provider = new DemoUsageProvider();

        var snapshot = await provider.GetUsageAsync(UsagePeriod.Last24Hours);

        Assert.Equal(ApiConnectionState.Connected, snapshot.ConnectionState);
        Assert.False(snapshot.IsFromCache);
        Assert.True(snapshot.Today.Requests > 0);
        Assert.Equal(snapshot.Today.InputTokens + snapshot.Today.OutputTokens, snapshot.Today.TotalTokens);
        Assert.NotEmpty(snapshot.Today.ModelBreakdown);
    }

    [Theory]
    [InlineData(UsagePeriod.Last24Hours, 24)]
    [InlineData(UsagePeriod.Last7Days, 7)]
    [InlineData(UsagePeriod.Last30Days, 30)]
    public async Task GetUsageAsync_HistoryLengthMatchesPeriod(UsagePeriod period, int expectedPoints)
    {
        var provider = new DemoUsageProvider();

        var snapshot = await provider.GetUsageAsync(period);

        Assert.Equal(expectedPoints, snapshot.History.Count);
    }

    [Fact]
    public async Task GetUsageAsync_ModelSharesAreWithinValidRange()
    {
        var provider = new DemoUsageProvider();

        var snapshot = await provider.GetUsageAsync(UsagePeriod.Last24Hours);

        Assert.All(snapshot.Today.ModelBreakdown, m => Assert.InRange(m.SharePercent, 0, 100));
    }
}
