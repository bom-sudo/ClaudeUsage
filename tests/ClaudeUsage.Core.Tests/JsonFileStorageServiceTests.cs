using ClaudeUsage.Core.Models;
using ClaudeUsage.Core.Services;
using Xunit;

namespace ClaudeUsage.Core.Tests;

public class JsonFileStorageServiceTests : IDisposable
{
    private readonly string _tempDir;

    public JsonFileStorageServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ClaudeUsageTests_" + Guid.NewGuid());
    }

    [Fact]
    public async Task LoadCachedSnapshotAsync_WithNoFile_ReturnsNull()
    {
        var storage = new JsonFileStorageService(_tempDir);

        var result = await storage.LoadCachedSnapshotAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAndLoadSnapshot_RoundTrips()
    {
        var storage = new JsonFileStorageService(_tempDir);
        var snapshot = new UsageSnapshot
        {
            Today = new UsageData { Requests = 42, InputTokens = 100, OutputTokens = 200 },
            Cost = new CostData { Today = 1.23m },
            RetrievedAt = DateTimeOffset.Parse("2026-08-19T12:00:00Z"),
        };

        await storage.SaveCachedSnapshotAsync(snapshot);
        var loaded = await storage.LoadCachedSnapshotAsync();

        Assert.NotNull(loaded);
        Assert.Equal(42, loaded!.Today.Requests);
        Assert.Equal(300, loaded.Today.TotalTokens);
        Assert.Equal(1.23m, loaded.Cost.Today);
    }

    [Fact]
    public async Task LoadSettingsAsync_WithNoFile_ReturnsDefaults()
    {
        var storage = new JsonFileStorageService(_tempDir);

        var settings = await storage.LoadSettingsAsync();

        Assert.Equal(new AppSettings().RefreshInterval, settings.RefreshInterval);
    }

    [Fact]
    public async Task SaveAndLoadSettings_RoundTrips()
    {
        var storage = new JsonFileStorageService(_tempDir);
        var settings = new AppSettings { DemoModeEnabled = false, ApiEndpoint = "https://example.com/usage", DailyTokenLimit = 1_000_000 };

        await storage.SaveSettingsAsync(settings);
        var loaded = await storage.LoadSettingsAsync();

        Assert.False(loaded.DemoModeEnabled);
        Assert.Equal("https://example.com/usage", loaded.ApiEndpoint);
        Assert.Equal(1_000_000, loaded.DailyTokenLimit);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
