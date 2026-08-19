using System.Threading;
using ClaudeUsage.Core.Models;

namespace ClaudeUsage.Core.Services;

/// <summary>
/// Source of usage data. The UI and <see cref="IUsageService"/> depend only on this abstraction,
/// never on a concrete provider, so the data source can be swapped without touching UI/ViewModel code.
/// </summary>
public interface IUsageProvider
{
    string Name { get; }

    Task<UsageSnapshot> GetUsageAsync(UsagePeriod historyPeriod, CancellationToken cancellationToken = default);
}
