using ClaudeUsage.Core.Models;
using Xunit;

namespace ClaudeUsage.Core.Tests;

public class UsageStateCalculatorTests
{
    [Theory]
    [InlineData(0, UsageState.Normal)]
    [InlineData(50, UsageState.Normal)]
    [InlineData(51, UsageState.Moderate)]
    [InlineData(75, UsageState.Moderate)]
    [InlineData(76, UsageState.Warning)]
    [InlineData(90, UsageState.Warning)]
    [InlineData(91, UsageState.Critical)]
    [InlineData(140, UsageState.Critical)]
    public void FromPercent_MapsToExpectedBand(double percent, UsageState expected)
    {
        Assert.Equal(expected, UsageStateCalculator.FromPercent(percent));
    }
}
