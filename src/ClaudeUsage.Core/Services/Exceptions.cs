namespace ClaudeUsage.Core.Services;

public abstract class UsageProviderException : Exception
{
    protected UsageProviderException(string message, Exception? inner = null) : base(message, inner) { }
}

public sealed class ApiUnauthorizedException : UsageProviderException
{
    public ApiUnauthorizedException(string message = "The API key was rejected.") : base(message) { }
}

public sealed class ApiRateLimitedException : UsageProviderException
{
    public TimeSpan? RetryAfter { get; }

    public ApiRateLimitedException(TimeSpan? retryAfter = null, string message = "The API is rate limiting requests.")
        : base(message)
    {
        RetryAfter = retryAfter;
    }
}

public sealed class ApiUnavailableException : UsageProviderException
{
    public ApiUnavailableException(string message = "The API is temporarily unavailable.", Exception? inner = null)
        : base(message, inner) { }
}

public sealed class InvalidUsageResponseException : UsageProviderException
{
    public InvalidUsageResponseException(string message = "The API returned an unexpected response.", Exception? inner = null)
        : base(message, inner) { }
}
