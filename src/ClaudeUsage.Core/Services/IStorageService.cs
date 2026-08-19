using System.Threading;
using ClaudeUsage.Core.Models;

namespace ClaudeUsage.Core.Services;

/// <summary>Local, non-secret cache: last usage snapshot and user preferences. Never used for credentials.</summary>
public interface IStorageService
{
    Task<UsageSnapshot?> LoadCachedSnapshotAsync(CancellationToken cancellationToken = default);
    Task SaveCachedSnapshotAsync(UsageSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
