using System.Net;
using System.Net.Http;
using System.Threading;
using ClaudeUsage.Core.Models;
using ClaudeUsage.Core.Services;

namespace ClaudeUsage.Core.Tests;

internal sealed class FakeStorageService : IStorageService
{
    public UsageSnapshot? Snapshot { get; set; }
    public AppSettings Settings { get; set; } = new();
    public int SaveSnapshotCallCount { get; private set; }

    public Task<UsageSnapshot?> LoadCachedSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(Snapshot);

    public Task SaveCachedSnapshotAsync(UsageSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        Snapshot = snapshot;
        SaveSnapshotCallCount++;
        return Task.CompletedTask;
    }

    public Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Settings);

    public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Settings = settings;
        return Task.CompletedTask;
    }
}

internal sealed class FakeNotificationService : INotificationService
{
    public List<int> ThresholdAlerts { get; } = [];
    public int ConnectionRestoredCount { get; private set; }

    public void ShowUsageThresholdAlert(int thresholdPercent) => ThresholdAlerts.Add(thresholdPercent);
    public void ShowConnectionRestored() => ConnectionRestoredCount++;
}

internal sealed class FakeSecretProvider : ISecretProvider
{
    private string? _apiKey = "fake-key";

    public Task<string?> GetApiKeyAsync() => Task.FromResult(_apiKey);
    public Task SetApiKeyAsync(string apiKey) { _apiKey = apiKey; return Task.CompletedTask; }
    public Task ClearApiKeyAsync() { _apiKey = null; return Task.CompletedTask; }
}

internal sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string? jsonBody = null) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(statusCode);
        if (jsonBody is not null)
        {
            response.Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
        }

        return Task.FromResult(response);
    }
}
