namespace ClaudeUsage.Core.Services;

/// <summary>
/// Secure storage for the API key. Implemented in the app layer on top of the Windows
/// Credential Locker (Windows.Security.Credentials.PasswordVault) — never plain text/JSON.
/// </summary>
public interface ISecretProvider
{
    Task<string?> GetApiKeyAsync();
    Task SetApiKeyAsync(string apiKey);
    Task ClearApiKeyAsync();
}
