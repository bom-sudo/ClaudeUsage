namespace ClaudeUsage.ViewModels;

/// <summary>Display-ready row for the Model Usage list. New models need no UI changes — the ItemsControl just gets another row.</summary>
public sealed class ModelUsageItem
{
    public required string DisplayName { get; init; }
    public double SharePercent { get; init; }
    public string SharePercentText => $"{Math.Round(SharePercent)}%";
    public long TotalTokens { get; init; }
}
