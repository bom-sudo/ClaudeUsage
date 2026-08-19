using ClaudeUsage.Core.Formatting;
using Xunit;

namespace ClaudeUsage.Core.Tests;

public class FormattersTests
{
    [Theory]
    [InlineData(999, "999")]
    [InlineData(1_200, "1.2K")]
    [InlineData(2_840_000, "2.84M")]
    [InlineData(1_500_000_000, "1.5B")]
    public void FormatTokenCount_ProducesCompactUnits(long tokens, string expected)
    {
        Assert.Equal(expected, Formatters.FormatTokenCount(tokens));
    }

    [Fact]
    public void FormatCost_UsesUsDollarFormat()
    {
        Assert.Equal("$4.82", Formatters.FormatCost(4.82m));
    }

    [Theory]
    [InlineData(12.4, "↑ 12.4%")]
    [InlineData(-3.1, "↓ 3.1%")]
    public void FormatSignedPercent_AddsDirectionalArrow(double percent, string expected)
    {
        Assert.Equal(expected, Formatters.FormatSignedPercent(percent));
    }

    [Fact]
    public void FormatRelativeTime_JustNow()
    {
        var now = DateTimeOffset.Parse("2026-08-19T12:00:00Z");
        Assert.Equal("just now", Formatters.FormatRelativeTime(now.AddSeconds(-2), now));
    }

    [Fact]
    public void FormatRelativeTime_SecondsAgo()
    {
        var now = DateTimeOffset.Parse("2026-08-19T12:00:00Z");
        Assert.Equal("30 seconds ago", Formatters.FormatRelativeTime(now.AddSeconds(-30), now));
    }

    [Fact]
    public void FormatRelativeTime_MinutesAgo()
    {
        var now = DateTimeOffset.Parse("2026-08-19T12:00:00Z");
        Assert.Equal("5 minutes ago", Formatters.FormatRelativeTime(now.AddMinutes(-5), now));
    }
}
