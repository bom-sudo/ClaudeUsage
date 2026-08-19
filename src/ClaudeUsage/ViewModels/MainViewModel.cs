using System.Collections.ObjectModel;
using ClaudeUsage.Core.Formatting;
using ClaudeUsage.Core.Models;
using ClaudeUsage.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;

namespace ClaudeUsage.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IUsageService _usageService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _relativeTimeTimer;

    [ObservableProperty] private bool isLoading = true;
    [ObservableProperty] private bool isOffline;
    [ObservableProperty] private string? errorMessage;

    [ObservableProperty] private string requestsText = "—";
    [ObservableProperty] private string tokensText = "—";
    [ObservableProperty] private string costText = "—";

    [ObservableProperty] private double usagePercent;
    [ObservableProperty] private string usagePercentText = "0%";
    [ObservableProperty] private UsageState usageState = UsageState.Normal;

    [ObservableProperty] private string costTodayText = "—";
    [ObservableProperty] private string costMonthToDateText = "—";
    [ObservableProperty] private string costProjectedText = "—";
    [ObservableProperty] private string costChangeText = "—";

    [ObservableProperty] private UsagePeriod selectedHistoryPeriod = UsagePeriod.Last24Hours;

    [ObservableProperty] private ApiConnectionState connectionState = ApiConnectionState.Offline;
    [ObservableProperty] private string connectionStateText = "Connecting…";
    [ObservableProperty] private string lastUpdatedText = "Never updated";

    public ObservableCollection<ModelUsageItem> ModelUsages { get; } = [];
    public ObservableCollection<UsageHistoryPoint> HistoryPoints { get; } = [];

    private DateTimeOffset _lastUpdatedAt = DateTimeOffset.MinValue;

    public MainViewModel(IUsageService usageService, DispatcherQueue dispatcherQueue)
    {
        _usageService = usageService;
        _dispatcherQueue = dispatcherQueue;

        _usageService.UsageUpdated += OnUsageUpdated;
        _usageService.ConnectionStateChanged += OnConnectionStateChanged;

        _relativeTimeTimer = _dispatcherQueue.CreateTimer();
        _relativeTimeTimer.Interval = TimeSpan.FromSeconds(1);
        _relativeTimeTimer.Tick += (_, _) => RefreshLastUpdatedText();
        _relativeTimeTimer.Start();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await _usageService.RefreshAsync(userInitiated: true);
    }

    partial void OnSelectedHistoryPeriodChanged(UsagePeriod value)
    {
        _usageService.HistoryPeriod = value;
        _ = _usageService.RefreshAsync(userInitiated: true);
    }

    private void OnConnectionStateChanged(object? sender, ApiConnectionState state)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            ConnectionState = state;
            IsOffline = state is ApiConnectionState.Offline or ApiConnectionState.Error or ApiConnectionState.Unauthorized or ApiConnectionState.RateLimited;
            ConnectionStateText = state switch
            {
                ApiConnectionState.Connected => "API Connected",
                ApiConnectionState.Connecting => "Connecting…",
                ApiConnectionState.Unauthorized => "Unauthorized",
                ApiConnectionState.RateLimited => "Rate Limited",
                ApiConnectionState.Error => "Connection Error",
                ApiConnectionState.Offline => "Offline",
                _ => "Unknown",
            };
        });
    }

    private void OnUsageUpdated(object? sender, UsageSnapshot snapshot)
    {
        _dispatcherQueue.TryEnqueue(() => Apply(snapshot));
    }

    private void Apply(UsageSnapshot snapshot)
    {
        IsLoading = false;
        ErrorMessage = snapshot.ErrorMessage;
        _lastUpdatedAt = snapshot.RetrievedAt;

        RequestsText = Formatters.FormatRequestCount(snapshot.Today.Requests);
        TokensText = Formatters.FormatTokenCount(snapshot.Today.TotalTokens);
        CostText = Formatters.FormatCost(snapshot.Today.EstimatedCostUsd);

        UsagePercent = Math.Clamp(snapshot.Today.LimitUsagePercent, 0, 100);
        UsagePercentText = Formatters.FormatPercent(snapshot.Today.LimitUsagePercent);
        UsageState = UsageStateCalculator.FromPercent(snapshot.Today.LimitUsagePercent);

        CostTodayText = Formatters.FormatCost(snapshot.Cost.Today);
        CostMonthToDateText = Formatters.FormatCost(snapshot.Cost.MonthToDate);
        CostProjectedText = Formatters.FormatCost(snapshot.Cost.ProjectedMonth);
        CostChangeText = $"{Formatters.FormatSignedPercent(snapshot.Cost.PercentChangeFromPreviousPeriod)} from last month";

        ModelUsages.Clear();
        foreach (var model in snapshot.Today.ModelBreakdown.OrderByDescending(m => m.SharePercent))
        {
            ModelUsages.Add(new ModelUsageItem
            {
                DisplayName = model.DisplayName,
                SharePercent = model.SharePercent,
                TotalTokens = model.TotalTokens,
            });
        }

        HistoryPoints.Clear();
        foreach (var point in snapshot.History)
        {
            HistoryPoints.Add(point);
        }

        RefreshLastUpdatedText();
    }

    private void RefreshLastUpdatedText()
    {
        if (_lastUpdatedAt == DateTimeOffset.MinValue)
        {
            return;
        }

        LastUpdatedText = $"Updated {Formatters.FormatRelativeTime(_lastUpdatedAt)}";
    }

    public void Dispose()
    {
        _usageService.UsageUpdated -= OnUsageUpdated;
        _usageService.ConnectionStateChanged -= OnConnectionStateChanged;
        _relativeTimeTimer.Stop();
    }
}
