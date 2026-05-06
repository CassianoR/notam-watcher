using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotamWatcher.Infrastructure.Configuration;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace NotamWatcher.Infrastructure.FaaApi;

/// <summary>
/// Typed HttpClient for the FAA NOTAM API.
/// The three-layer Polly pipeline is registered in <see cref="InfrastructureServiceExtensions"/>:
///   1. Timeout (innermost) – kills hung requests after 5 s so retries aren't blocked by a stalled socket.
///   2. Retry with exponential backoff + jitter – 3 attempts, 1 s / 2 s / 4 s base delays.
///      Jitter prevents thundering-herd self-DDoS when multiple fetch cycles miss simultaneously.
///      Only retries transient errors (network failures, 5xx, 429 Too Many Requests).
///   3. Circuit breaker (outermost) – opens after 5 consecutive failures, stays open for 30 s.
///      When open, this method returns null immediately; the background service logs a warning
///      and skips the cycle rather than queue-flooding the FAA endpoint.
/// </summary>
public sealed class FaaNotamClient : IFaaNotamClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly FaaApiOptions _options;
    private readonly ILogger<FaaNotamClient> _logger;

    public FaaNotamClient(
        HttpClient http,
        IOptions<FaaApiOptions> options,
        ILogger<FaaNotamClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FaaNotamResponse?> FetchAsync(
        IReadOnlyCollection<string> icaoCodes,
        int pageNum = 1,
        CancellationToken ct = default)
    {
        var locationParam = string.Join(",", icaoCodes);
        var url = $"{_options.BaseUrl}/notams" +
                  $"?icaoLocation={Uri.EscapeDataString(locationParam)}" +
                  $"&pageSize={_options.PageSize}" +
                  $"&pageNum={pageNum}";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("client_id", _options.ApiKey);

            using var response = await _http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "FAA API returned {StatusCode} for locations {Locations}",
                    response.StatusCode, locationParam);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonSerializer.DeserializeAsync<FaaNotamResponse>(stream, JsonOptions, ct);
        }
        catch (BrokenCircuitException ex)
        {
            // Circuit is open — log and let the background service skip this cycle.
            _logger.LogWarning(ex, "FAA API circuit breaker is open, skipping fetch cycle");
            return null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Polly timeout policy fired — not a host shutdown, so log as warning not error.
            _logger.LogWarning("FAA API request timed out for locations {Locations}", locationParam);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching NOTAMs for {Locations}", locationParam);
            return null;
        }
    }
}
