using ClaudeUsage.Core.Services;
using Windows.ApplicationModel;

namespace ClaudeUsage.Services;

/// <summary>
/// Launch-at-login via the packaged-app StartupTask API (declared in Package.appxmanifest as
/// "ClaudeUsageStartup"). This is the mechanism Windows expects for MSIX apps — no registry Run-key hacks.
/// </summary>
public sealed class StartupTaskService : IStartupService
{
    private const string TaskId = "ClaudeUsageStartup";

    public async Task<bool> IsEnabledAsync()
    {
        var task = await StartupTask.GetAsync(TaskId);
        return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
    }

    public async Task<bool> RequestEnableAsync()
    {
        var task = await StartupTask.GetAsync(TaskId);
        var state = task.State;

        if (state == StartupTaskState.Disabled)
        {
            state = await task.RequestEnableAsync();
        }

        return state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
    }

    public async Task DisableAsync()
    {
        var task = await StartupTask.GetAsync(TaskId);
        task.Disable();
    }
}
