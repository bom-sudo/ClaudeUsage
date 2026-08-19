using ClaudeUsage.Core.Services;
using Windows.Security.Credentials;

namespace ClaudeUsage.Services;

/// <summary>
/// Stores the API key in the Windows Credential Locker (PasswordVault) — encrypted at rest by
/// Windows, scoped to this app's package identity. The key is never written to disk as plain text.
/// </summary>
public sealed class CredentialVaultStore : ISecretProvider
{
    private const string Resource = "ClaudeUsage.ApiKey";
    private const string UserName = "default";

    public Task<string?> GetApiKeyAsync()
    {
        try
        {
            var vault = new PasswordVault();
            var credential = vault.Retrieve(Resource, UserName);
            credential.RetrievePassword();
            return Task.FromResult<string?>(credential.Password);
        }
        catch (Exception)
        {
            // Thrown by PasswordVault when no matching credential exists yet.
            return Task.FromResult<string?>(null);
        }
    }

    public Task SetApiKeyAsync(string apiKey)
    {
        var vault = new PasswordVault();
        ClearExisting(vault);
        vault.Add(new PasswordCredential(Resource, UserName, apiKey));
        return Task.CompletedTask;
    }

    public Task ClearApiKeyAsync()
    {
        var vault = new PasswordVault();
        ClearExisting(vault);
        return Task.CompletedTask;
    }

    private static void ClearExisting(PasswordVault vault)
    {
        try
        {
            var existing = vault.Retrieve(Resource, UserName);
            vault.Remove(existing);
        }
        catch (Exception)
        {
            // No existing credential to remove.
        }
    }
}
