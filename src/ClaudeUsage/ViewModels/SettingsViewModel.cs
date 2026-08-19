using System.Collections.ObjectModel;
using System.Net.Http;
using ClaudeUsage.Core.Models;
using ClaudeUsage.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudeUsage.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IStorageService _storage;
    private readonly ISecretProvider _secretProvider;
    private readonly IStartupService _startupService;
    private readonly IUsageService _usageService;
    private readonly ClaudeUsageProvider _testProvider;

    [ObservableProperty] private string apiEndpoint = string.Empty;
    [ObservableProperty] private string apiKeyInput = string.Empty;
    [ObservableProperty] private bool hasStoredApiKey;
    [ObservableProperty] private bool demoModeEnabled = true;

    [ObservableProperty] private AppTheme theme = AppTheme.System;
    [ObservableProperty] private double transparencyLevel = 0.8;
    [ObservableProperty] private bool animationsEnabled = true;
    [ObservableProperty] private bool compactMode;

    [ObservableProperty] private bool startWithWindows;

    [ObservableProperty] private bool notificationsEnabled = true;
    public ObservableCollection<NotificationThresholdOption> NotificationThresholdOptions { get; } = [];

    [ObservableProperty] private double dailyTokenLimit = 5_000_000;
    [ObservableProperty] private RefreshInterval refreshInterval = RefreshInterval.Minutes5;
    [ObservableProperty] private bool verboseLoggingEnabled;

    [ObservableProperty] private string connectionTestStatusText = string.Empty;
    [ObservableProperty] private bool isTestingConnection;
    [ObservableProperty] private bool isSaved;

    public SettingsViewModel(
        IStorageService storage,
        ISecretProvider secretProvider,
        IStartupService startupService,
        IUsageService usageService)
    {
        _storage = storage;
        _secretProvider = secretProvider;
        _startupService = startupService;
        _usageService = usageService;

        // Deliberately NOT the same ClaudeUsageProvider instance the running UsageService uses for scheduled
        // refreshes — otherwise testing an unsaved endpoint here would redirect live data collection to it.
        _testProvider = new ClaudeUsageProvider(new HttpClient { Timeout = TimeSpan.FromSeconds(10) }, secretProvider);

        foreach (var percent in new[] { 50, 70, 80, 90, 95 })
        {
            NotificationThresholdOptions.Add(new NotificationThresholdOption(percent, isSelected: false));
        }
    }

    public async Task LoadAsync()
    {
        var settings = await _storage.LoadSettingsAsync();

        ApiEndpoint = settings.ApiEndpoint;
        DemoModeEnabled = settings.DemoModeEnabled;
        Theme = settings.Theme;
        TransparencyLevel = settings.TransparencyLevel;
        AnimationsEnabled = settings.AnimationsEnabled;
        CompactMode = settings.CompactMode;
        StartWithWindows = settings.StartWithWindows;
        NotificationsEnabled = settings.NotificationsEnabled;
        DailyTokenLimit = settings.DailyTokenLimit;
        RefreshInterval = settings.RefreshInterval;
        VerboseLoggingEnabled = settings.VerboseLoggingEnabled;

        foreach (var option in NotificationThresholdOptions)
        {
            option.IsSelected = settings.NotificationThresholds.Contains(option.Percent);
        }

        HasStoredApiKey = !string.IsNullOrEmpty(await _secretProvider.GetApiKeyAsync());
        IsSaved = true;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var settings = new AppSettings
        {
            ApiEndpoint = ApiEndpoint.Trim(),
            DemoModeEnabled = DemoModeEnabled,
            Theme = Theme,
            TransparencyLevel = TransparencyLevel,
            AnimationsEnabled = AnimationsEnabled,
            CompactMode = CompactMode,
            StartWithWindows = StartWithWindows,
            NotificationsEnabled = NotificationsEnabled,
            NotificationThresholds = NotificationThresholdOptions.Where(o => o.IsSelected).Select(o => o.Percent).ToList(),
            DailyTokenLimit = DailyTokenLimit,
            RefreshInterval = RefreshInterval,
            VerboseLoggingEnabled = VerboseLoggingEnabled,
        };

        if (!string.IsNullOrWhiteSpace(ApiKeyInput))
        {
            await _secretProvider.SetApiKeyAsync(ApiKeyInput.Trim());
            ApiKeyInput = string.Empty;
            HasStoredApiKey = true;
        }

        await _storage.SaveSettingsAsync(settings);
        _usageService.ApplySettings(settings);

        if (StartWithWindows)
        {
            StartWithWindows = await _startupService.RequestEnableAsync();
        }
        else
        {
            await _startupService.DisableAsync();
        }

        IsSaved = true;
    }

    [RelayCommand]
    private void ClearApiKey()
    {
        _ = _secretProvider.ClearApiKeyAsync();
        HasStoredApiKey = false;
        ApiKeyInput = string.Empty;
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        IsTestingConnection = true;
        ConnectionTestStatusText = "Testing connection…";

        try
        {
            if (!Uri.TryCreate(ApiEndpoint, UriKind.Absolute, out var uri))
            {
                ConnectionTestStatusText = "Enter a valid API endpoint URL first.";
                return;
            }

            if (!string.IsNullOrWhiteSpace(ApiKeyInput))
            {
                await _secretProvider.SetApiKeyAsync(ApiKeyInput.Trim());
                HasStoredApiKey = true;
            }

            _testProvider.Endpoint = uri;
            var snapshot = await _testProvider.GetUsageAsync(UsagePeriod.Last24Hours);
            ConnectionTestStatusText = $"Connected — {snapshot.Today.Requests:N0} requests today.";
        }
        catch (ApiUnauthorizedException)
        {
            ConnectionTestStatusText = "Unauthorized — check the API key.";
        }
        catch (ApiRateLimitedException)
        {
            ConnectionTestStatusText = "Rate limited — try again shortly.";
        }
        catch (UsageProviderException ex)
        {
            ConnectionTestStatusText = ex.Message;
        }
        catch (Exception)
        {
            ConnectionTestStatusText = "Could not connect. Check the endpoint and network connection.";
        }
        finally
        {
            IsTestingConnection = false;
        }
    }
}
