namespace ClaudeUsage.Core.Services;

/// <summary>Registers/unregisters launch-at-login. Implemented via the packaged-app StartupTask API (Windows.ApplicationModel.StartupTask) — no registry hacks.</summary>
public interface IStartupService
{
    Task<bool> IsEnabledAsync();

    /// <returns>True if startup is enabled after the request (the user may have disabled it in Windows Settings, which must be reflected back).</returns>
    Task<bool> RequestEnableAsync();

    Task DisableAsync();
}
