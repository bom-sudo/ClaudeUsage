using System.Net;
using System.Net.Http;
using ClaudeUsage.Core.Models;
using ClaudeUsage.Core.Services;
using Xunit;

namespace ClaudeUsage.Core.Tests;

public class UsageServiceTests
{
    private static UsageService CreateService(
        FakeStorageService storage,
        FakeNotificationService? notifications = null,
        HttpStatusCode liveStatusCode = HttpStatusCode.OK,
        string? liveJsonBody = null)
    {
        var demoProvider = new DemoUsageProvider();
        var httpClient = new HttpClient(new StubHttpMessageHandler(liveStatusCode, liveJsonBody));
        var liveProvider = new ClaudeUsageProvider(httpClient, new FakeSecretProvider());

        return new UsageService(demoProvider, liveProvider, storage, notifications);
    }

    [Fact]
    public async Task InitializeAsync_WithCachedSnapshot_SurfacesItBeforeRefreshing()
    {
        var storage = new FakeStorageService
        {
            Settings = new AppSettings { DemoModeEnabled = true, RefreshInterval = RefreshInterval.Disabled },
            Snapshot = new UsageSnapshot
            {
                Today = new UsageData { Requests = 7 },
                Cost = new CostData(),
                RetrievedAt = DateTimeOffset.Now.AddHours(-1),
            },
        };
        var events = new List<UsageSnapshot>();
        using var service = CreateService(storage);
        service.UsageUpdated += (_, snap) => events.Add(snap);

        await service.InitializeAsync();

        Assert.True(events.Count >= 2);
        Assert.True(events[0].IsFromCache);
        Assert.Equal(7, events[0].Today.Requests);
        Assert.False(events[^1].IsFromCache);
    }

    [Fact]
    public async Task RefreshAsync_DemoMode_UpdatesCurrentAndPersistsCache()
    {
        var storage = new FakeStorageService { Settings = new AppSettings { DemoModeEnabled = true } };
        using var service = CreateService(storage);
        service.ApplySettings(storage.Settings);

        await service.RefreshAsync();

        Assert.NotNull(service.Current);
        Assert.Equal(ApiConnectionState.Connected, service.ConnectionState);
        Assert.Equal(1, storage.SaveSnapshotCallCount);
    }

    [Fact]
    public async Task RefreshAsync_RapidManualCalls_AreDebounced()
    {
        var storage = new FakeStorageService { Settings = new AppSettings { DemoModeEnabled = true } };
        using var service = CreateService(storage);
        service.ApplySettings(storage.Settings);

        await service.RefreshAsync(userInitiated: true);
        await service.RefreshAsync(userInitiated: true);

        Assert.Equal(1, storage.SaveSnapshotCallCount);
    }

    [Fact]
    public async Task RefreshAsync_LiveModeUnauthorized_SetsUnauthorizedStateWithoutThrowing()
    {
        var storage = new FakeStorageService
        {
            Settings = new AppSettings { DemoModeEnabled = false, ApiEndpoint = "https://example.invalid/usage" },
        };
        using var service = CreateService(storage, liveStatusCode: HttpStatusCode.Unauthorized);
        service.ApplySettings(storage.Settings);

        await service.RefreshAsync();

        Assert.Equal(ApiConnectionState.Unauthorized, service.ConnectionState);
    }

    [Fact]
    public async Task RefreshAsync_LiveModeRateLimited_SetsRateLimitedState()
    {
        var storage = new FakeStorageService
        {
            Settings = new AppSettings { DemoModeEnabled = false, ApiEndpoint = "https://example.invalid/usage" },
        };
        using var service = CreateService(storage, liveStatusCode: HttpStatusCode.TooManyRequests);
        service.ApplySettings(storage.Settings);

        await service.RefreshAsync();

        Assert.Equal(ApiConnectionState.RateLimited, service.ConnectionState);
    }

    [Fact]
    public async Task RefreshAsync_ThresholdCrossed_FiresOnlyOncePerDay()
    {
        var storage = new FakeStorageService
        {
            Settings = new AppSettings { DemoModeEnabled = true, NotificationsEnabled = true, NotificationThresholds = [1] },
        };
        var notifications = new FakeNotificationService();
        using var service = CreateService(storage, notifications);
        service.ApplySettings(storage.Settings);

        await service.RefreshAsync(userInitiated: true);
        await Task.Delay(2100);
        await service.RefreshAsync(userInitiated: true);

        Assert.Single(notifications.ThresholdAlerts);
    }

    [Fact]
    public async Task RefreshAsync_MissingLiveEndpoint_SetsErrorStateWithoutThrowing()
    {
        var storage = new FakeStorageService
        {
            Settings = new AppSettings { DemoModeEnabled = false, ApiEndpoint = "" },
        };
        using var service = CreateService(storage);
        service.ApplySettings(storage.Settings);

        await service.RefreshAsync();

        Assert.Equal(ApiConnectionState.Error, service.ConnectionState);
    }
}
