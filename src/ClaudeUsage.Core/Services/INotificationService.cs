namespace ClaudeUsage.Core.Services;

/// <summary>Platform toast notifications. Implemented in the app layer via Windows App SDK's AppNotificationManager.</summary>
public interface INotificationService
{
    void ShowUsageThresholdAlert(int thresholdPercent);
    void ShowConnectionRestored();
}
