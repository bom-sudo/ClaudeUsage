using ClaudeUsage.Core.Services;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace ClaudeUsage.Services;

/// <summary>Native Windows 11 toast notifications via the Windows App SDK. Registered once at app startup.</summary>
public sealed class ToastNotificationService : INotificationService, IDisposable
{
    public ToastNotificationService()
    {
        AppNotificationManager.Default.Register();
    }

    public void ShowUsageThresholdAlert(int thresholdPercent)
    {
        var notification = new AppNotificationBuilder()
            .AddText("Usage Alert")
            .AddText($"Claude usage has reached {thresholdPercent}%.")
            .BuildNotification();

        AppNotificationManager.Default.Show(notification);
    }

    public void ShowConnectionRestored()
    {
        var notification = new AppNotificationBuilder()
            .AddText("ClaudeUsage")
            .AddText("Connection restored — usage data is up to date again.")
            .BuildNotification();

        AppNotificationManager.Default.Show(notification);
    }

    public void Dispose() => AppNotificationManager.Default.Unregister();
}
