using CommunityToolkit.Mvvm.ComponentModel;

namespace ClaudeUsage.ViewModels;

public sealed partial class NotificationThresholdOption : ObservableObject
{
    public int Percent { get; }
    public string Label => $"{Percent}%";

    [ObservableProperty] private bool isSelected;

    public NotificationThresholdOption(int percent, bool isSelected)
    {
        Percent = percent;
        this.isSelected = isSelected;
    }
}
