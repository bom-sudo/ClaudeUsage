using System.Net.Http;
using ClaudeUsage.Core.Models;
using ClaudeUsage.Core.Services;
using ClaudeUsage.Services;
using ClaudeUsage.ViewModels;
using ClaudeUsage.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace ClaudeUsage;

/// <summary>Composition root: wires Core services to their Windows-specific implementations and owns app-level lifetime (main window, settings window, tray icon).</summary>
public partial class App : Application
{
    private IServiceProvider _services = null!;
    private IUsageService _usageService = null!;
    private ILogger<App> _logger = null!;

    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;
    private TrayIconService? _trayIcon;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _services = BuildServiceProvider();
        _logger = _services.GetRequiredService<ILogger<App>>();
        _usageService = _services.GetRequiredService<IUsageService>();

        _mainWindow = new MainWindow(_services.GetRequiredService<MainViewModel>(), OpenSettings);
        _mainWindow.Activate();

        var iconUri = new Uri("ms-appx:///Assets/AppIcon.ico");
        _trayIcon = new TrayIconService(iconUri);
        _trayIcon.OpenRequested += ShowMainWindow;
        _trayIcon.RefreshRequested += () => _ = _usageService.RefreshAsync(userInitiated: true);
        _trayIcon.SettingsRequested += OpenSettings;
        _trayIcon.PauseAutoRefreshToggled += paused => _ = OnPauseAutoRefreshToggledAsync(paused);
        _trayIcon.ExitRequested += ExitApplication;

        _usageService.UsageUpdated += (_, snapshot) => _trayIcon.UpdateStatus(_usageService.ConnectionState, snapshot.Today.LimitUsagePercent);
        _usageService.ConnectionStateChanged += (_, state) => _trayIcon.UpdateStatus(state, _usageService.Current?.Today.LimitUsagePercent ?? 0);

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            await ApplyThemeAsync();
            await _usageService.InitializeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Startup initialization failed.");
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.AppWindow.Show();
        _mainWindow.Activate();
    }

    private void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_services.GetRequiredService<SettingsViewModel>());
        _settingsWindow.Closed += async (_, _) =>
        {
            _settingsWindow = null;
            await ApplyThemeAsync();
        };
        _settingsWindow.Activate();
    }

    private async Task OnPauseAutoRefreshToggledAsync(bool paused)
    {
        var storage = _services.GetRequiredService<IStorageService>();
        var settings = await storage.LoadSettingsAsync();

        if (paused)
        {
            settings.RefreshInterval = RefreshInterval.Disabled;
        }

        _usageService.ApplySettings(settings);
    }

    private async Task ApplyThemeAsync()
    {
        var storage = _services.GetRequiredService<IStorageService>();
        var settings = await storage.LoadSettingsAsync();

        var elementTheme = settings.Theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };

        if (_mainWindow?.Content is FrameworkElement mainRoot)
        {
            mainRoot.RequestedTheme = elementTheme;
        }

        if (_settingsWindow?.Content is FrameworkElement settingsRoot)
        {
            settingsRoot.RequestedTheme = elementTheme;
        }
    }

    private void ExitApplication()
    {
        _trayIcon?.Dispose();
        _mainWindow?.ForceClose();
        (_services as IDisposable)?.Dispose();
        Exit();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unhandled exception.");
        e.Handled = true;
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddDebug();
#if DEBUG
            builder.SetMinimumLevel(LogLevel.Debug);
#else
            builder.SetMinimumLevel(LogLevel.Warning);
#endif
        });

        var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;

        services.AddSingleton<IStorageService>(sp =>
            new JsonFileStorageService(localFolder, sp.GetRequiredService<ILogger<JsonFileStorageService>>()));
        services.AddSingleton<ISecretProvider, CredentialVaultStore>();
        services.AddSingleton<INotificationService, ToastNotificationService>();
        services.AddSingleton<IStartupService, StartupTaskService>();

        services.AddSingleton<DemoUsageProvider>();
        services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromSeconds(15) });
        services.AddSingleton(sp => new ClaudeUsageProvider(
            sp.GetRequiredService<HttpClient>(),
            sp.GetRequiredService<ISecretProvider>(),
            sp.GetRequiredService<ILogger<ClaudeUsageProvider>>()));

        services.AddSingleton<IUsageService>(sp => new UsageService(
            sp.GetRequiredService<DemoUsageProvider>(),
            sp.GetRequiredService<ClaudeUsageProvider>(),
            sp.GetRequiredService<IStorageService>(),
            sp.GetRequiredService<INotificationService>(),
            sp.GetRequiredService<ILogger<UsageService>>()));

        services.AddSingleton(_ => DispatcherQueue.GetForCurrentThread());
        services.AddSingleton(sp => new MainViewModel(
            sp.GetRequiredService<IUsageService>(),
            sp.GetRequiredService<DispatcherQueue>()));

        services.AddTransient(sp => new SettingsViewModel(
            sp.GetRequiredService<IStorageService>(),
            sp.GetRequiredService<ISecretProvider>(),
            sp.GetRequiredService<IStartupService>(),
            sp.GetRequiredService<IUsageService>()));

        return services.BuildServiceProvider();
    }
}
