using ClaudeUsage.Core.Models;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;

namespace ClaudeUsage.Services;

/// <summary>
/// Windows system tray icon and its right-click menu (Open / Refresh / Settings / Pause auto-refresh / Exit),
/// built on H.NotifyIcon.WinUI. Kept lightweight — no window is created for this, just the shell icon.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly TaskbarIcon _taskbarIcon;
    private readonly MenuFlyoutItem _statusItem;
    private readonly MenuFlyoutItem _usageItem;
    private readonly MenuFlyoutItem _pauseItem;
    private bool _autoRefreshPaused;

    public event Action? OpenRequested;
    public event Action? RefreshRequested;
    public event Action? SettingsRequested;
    public event Action<bool>? PauseAutoRefreshToggled;
    public event Action? ExitRequested;

    public TrayIconService(Uri iconUri)
    {
        _statusItem = new MenuFlyoutItem { Text = "● API Connected", IsEnabled = false };
        _usageItem = new MenuFlyoutItem { Text = "Usage Today: —", IsEnabled = false };

        var openItem = new MenuFlyoutItem { Text = "Open Widget" };
        openItem.Click += (_, _) => OpenRequested?.Invoke();

        var refreshItem = new MenuFlyoutItem { Text = "Refresh" };
        refreshItem.Click += (_, _) => RefreshRequested?.Invoke();

        var settingsItem = new MenuFlyoutItem { Text = "Settings" };
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke();

        _pauseItem = new MenuFlyoutItem { Text = "Pause Auto Refresh" };
        _pauseItem.Click += (_, _) =>
        {
            _autoRefreshPaused = !_autoRefreshPaused;
            _pauseItem.Text = _autoRefreshPaused ? "Resume Auto Refresh" : "Pause Auto Refresh";
            PauseAutoRefreshToggled?.Invoke(_autoRefreshPaused);
        };

        var exitItem = new MenuFlyoutItem { Text = "Exit" };
        exitItem.Click += (_, _) => ExitRequested?.Invoke();

        var menu = new MenuFlyout();
        menu.Items.Add(_statusItem);
        menu.Items.Add(_usageItem);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(openItem);
        menu.Items.Add(refreshItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(exitItem);

        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = "ClaudeUsage — Claude Usage Monitor",
            IconSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(iconUri),
            ContextFlyout = menu,
        };
        _taskbarIcon.ForceCreate();
    }

    public void UpdateStatus(ApiConnectionState state, double usagePercent)
    {
        _statusItem.Text = state switch
        {
            ApiConnectionState.Connected => "● API Connected",
            ApiConnectionState.Connecting => "● Connecting…",
            ApiConnectionState.Unauthorized => "● Unauthorized",
            ApiConnectionState.RateLimited => "● Rate Limited",
            ApiConnectionState.Error => "● Connection Error",
            ApiConnectionState.Offline => "● Offline",
            _ => "● Unknown",
        };
        _usageItem.Text = $"Usage Today: {Math.Round(usagePercent)}%";
    }

    public void Dispose() => _taskbarIcon.Dispose();
}
