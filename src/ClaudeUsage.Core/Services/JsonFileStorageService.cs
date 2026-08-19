using System.Text.Json;
using System.Threading;
using ClaudeUsage.Core.Models;
using Microsoft.Extensions.Logging;

namespace ClaudeUsage.Core.Services;

/// <summary>
/// JSON-on-disk cache under a caller-supplied folder (the app passes ApplicationData's local folder;
/// tests pass a temp directory). Writes go through a temp file + move so a crash mid-write can't corrupt the cache.
/// </summary>
public sealed class JsonFileStorageService : IStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly string _snapshotPath;
    private readonly string _settingsPath;
    private readonly ILogger<JsonFileStorageService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonFileStorageService(string dataFolder, ILogger<JsonFileStorageService>? logger = null)
    {
        Directory.CreateDirectory(dataFolder);
        _snapshotPath = Path.Combine(dataFolder, "usage-cache.json");
        _settingsPath = Path.Combine(dataFolder, "settings.json");
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<JsonFileStorageService>.Instance;
    }

    public async Task<UsageSnapshot?> LoadCachedSnapshotAsync(CancellationToken cancellationToken = default)
        => await ReadAsync<UsageSnapshot>(_snapshotPath, cancellationToken).ConfigureAwait(false);

    public Task SaveCachedSnapshotAsync(UsageSnapshot snapshot, CancellationToken cancellationToken = default)
        => WriteAsync(_snapshotPath, snapshot, cancellationToken);

    public async Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
        => await ReadAsync<AppSettings>(_settingsPath, cancellationToken).ConfigureAwait(false) ?? new AppSettings();

    public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
        => WriteAsync(_settingsPath, settings, cancellationToken);

    private async Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Cache file {Path} was corrupt and will be ignored.", Path.GetFileName(path));
            return default;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task WriteAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tempPath = path + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            _lock.Release();
        }
    }
}
