using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using ClaudeUsage.Core.Models;
using Microsoft.Extensions.Logging;

namespace ClaudeUsage.Core.Services;

/// <summary>
/// Live usage provider. IMPORTANT: Anthropic does not currently publish a public, per-key usage/billing
/// query API for end users. This provider does not fabricate one — instead it is a generic HTTPS JSON
/// client you point at any endpoint that returns the <see cref="UsageResponseDto"/> shape below (a small
/// internal proxy, a LiteLLM/gateway usage export, an admin API you already have, etc.). Until you have
/// such an endpoint, use Demo Mode; this class exists so wiring in a real one later requires no UI changes.
/// </summary>
public sealed class ClaudeUsageProvider : IUsageProvider
{
    public string Name => "Claude API";

    /// <summary>The usage endpoint to call. Set from <see cref="Models.AppSettings.ApiEndpoint"/> before each use.</summary>
    public Uri? Endpoint { get; set; }

    private readonly HttpClient _httpClient;
    private readonly ISecretProvider _secretProvider;
    private readonly ILogger<ClaudeUsageProvider> _logger;

    public ClaudeUsageProvider(HttpClient httpClient, ISecretProvider secretProvider, ILogger<ClaudeUsageProvider>? logger = null)
    {
        _httpClient = httpClient;
        _secretProvider = secretProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ClaudeUsageProvider>.Instance;
    }

    public async Task<UsageSnapshot> GetUsageAsync(UsagePeriod historyPeriod, CancellationToken cancellationToken = default)
    {
        if (Endpoint is null)
        {
            throw new ApiUnavailableException("No API endpoint is configured. Set one in Settings or enable Demo Mode.");
        }

        var apiKey = await _secretProvider.GetApiKeyAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ApiUnauthorizedException("No API key is stored. Add one in Settings.");
        }

        var periodParam = historyPeriod switch
        {
            UsagePeriod.Last24Hours => "24h",
            UsagePeriod.Last7Days => "7d",
            UsagePeriod.Last30Days => "30d",
            _ => "24h",
        };

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(Endpoint, $"?period={periodParam}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ApiUnavailableException("The request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiUnavailableException("Could not reach the API endpoint.", ex);
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                case HttpStatusCode.Forbidden:
                    throw new ApiUnauthorizedException();
                case HttpStatusCode.TooManyRequests:
                    var retryAfter = response.Headers.RetryAfter?.Delta;
                    throw new ApiRateLimitedException(retryAfter);
                case >= HttpStatusCode.InternalServerError:
                    throw new ApiUnavailableException($"The API returned {(int)response.StatusCode}.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidUsageResponseException($"Unexpected status code {(int)response.StatusCode}.");
            }

            UsageResponseDto? dto;
            try
            {
                dto = await response.Content.ReadFromJsonAsync<UsageResponseDto>(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new InvalidUsageResponseException("The response body was not valid JSON.", ex);
            }

            if (dto is null)
            {
                throw new InvalidUsageResponseException("The response body was empty.");
            }

            return Map(dto);
        }
    }

    private static UsageSnapshot Map(UsageResponseDto dto)
    {
        var today = dto.Today ?? throw new InvalidUsageResponseException("Response is missing the 'today' section.");

        return new UsageSnapshot
        {
            Today = new UsageData
            {
                Timestamp = DateTimeOffset.Now,
                Requests = today.Requests,
                InputTokens = today.InputTokens,
                OutputTokens = today.OutputTokens,
                EstimatedCostUsd = today.EstimatedCostUsd,
                LimitUsagePercent = today.LimitUsagePercent,
                ModelBreakdown = today.Models?.Select(m => new ModelUsage
                {
                    ModelId = m.Id,
                    DisplayName = m.DisplayName,
                    TotalTokens = m.TotalTokens,
                    SharePercent = m.SharePercent,
                }).ToList() ?? [],
            },
            Cost = dto.Cost is null
                ? new CostData()
                : new CostData
                {
                    Today = dto.Cost.Today,
                    MonthToDate = dto.Cost.MonthToDate,
                    ProjectedMonth = dto.Cost.ProjectedMonth,
                    PercentChangeFromPreviousPeriod = dto.Cost.PercentChangeFromPreviousPeriod,
                },
            History = dto.History?.Select(h => new UsageHistoryPoint
            {
                Timestamp = h.Timestamp,
                UsagePercent = h.UsagePercent,
                TotalTokens = h.TotalTokens,
            }).ToList() ?? [],
            ConnectionState = ApiConnectionState.Connected,
            RetrievedAt = DateTimeOffset.Now,
            IsFromCache = false,
        };
    }

    // DTOs describing the expected JSON contract for a compatible endpoint.
    private sealed class UsageResponseDto
    {
        [JsonPropertyName("today")] public TodayDto? Today { get; set; }
        [JsonPropertyName("cost")] public CostDto? Cost { get; set; }
        [JsonPropertyName("history")] public List<HistoryPointDto>? History { get; set; }
    }

    private sealed class TodayDto
    {
        [JsonPropertyName("requests")] public int Requests { get; set; }
        [JsonPropertyName("inputTokens")] public long InputTokens { get; set; }
        [JsonPropertyName("outputTokens")] public long OutputTokens { get; set; }
        [JsonPropertyName("estimatedCostUsd")] public decimal EstimatedCostUsd { get; set; }
        [JsonPropertyName("limitUsagePercent")] public double LimitUsagePercent { get; set; }
        [JsonPropertyName("models")] public List<ModelDto>? Models { get; set; }
    }

    private sealed class ModelDto
    {
        [JsonPropertyName("id")] public required string Id { get; set; }
        [JsonPropertyName("displayName")] public required string DisplayName { get; set; }
        [JsonPropertyName("totalTokens")] public long TotalTokens { get; set; }
        [JsonPropertyName("sharePercent")] public double SharePercent { get; set; }
    }

    private sealed class CostDto
    {
        [JsonPropertyName("today")] public decimal Today { get; set; }
        [JsonPropertyName("monthToDate")] public decimal MonthToDate { get; set; }
        [JsonPropertyName("projectedMonth")] public decimal ProjectedMonth { get; set; }
        [JsonPropertyName("percentChangeFromPreviousPeriod")] public double PercentChangeFromPreviousPeriod { get; set; }
    }

    private sealed class HistoryPointDto
    {
        [JsonPropertyName("timestamp")] public DateTimeOffset Timestamp { get; set; }
        [JsonPropertyName("usagePercent")] public double UsagePercent { get; set; }
        [JsonPropertyName("totalTokens")] public long TotalTokens { get; set; }
    }
}
