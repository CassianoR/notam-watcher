using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NotamWatcher.Infrastructure.Configuration;
using NotamWatcher.Infrastructure.FaaApi;
using NotamWatcher.Infrastructure.Persistence;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace NotamWatcher.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FaaApiOptions>(configuration.GetSection(FaaApiOptions.Section));
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.Section));

        services.AddDbContext<AppDbContext>((sp, opts) =>
        {
            var dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            opts.UseSqlite($"Data Source={dbOptions.Path}");
        });

        services.AddScoped<NotamRepository>();
        services.AddScoped<WatchedRouteRepository>();

        services
            .AddHttpClient<IFaaNotamClient, FaaNotamClient>()
            .AddPolicyHandler(BuildResiliencePipeline());

        return services;
    }

    /// <summary>
    /// Three-layer Polly pipeline, applied outermost-first:
    ///
    ///   [Circuit Breaker] → [Retry + Backoff] → [Timeout] → HttpClient
    ///
    /// Policy choices:
    ///   Timeout  5 s  – FAA API P95 latency is well under 2 s; 5 s gives headroom for
    ///                    transient slowness while still releasing the thread promptly.
    ///
    ///   Retry    3 attempts, exponential backoff 1/2/4 s + ±20% jitter.
    ///            Jitter (Polly.Contrib.WaitAndRetry style via manual calculation) prevents
    ///            synchronized retry storms when multiple fetch cycles hit the same outage.
    ///            Only 5xx and 429 (rate-limit) are retried; 4xx are considered caller errors.
    ///
    ///   Circuit  Opens after 5 consecutive failures, stays open for 30 s.
    ///   Breaker  30 s gives the FAA endpoint time to recover from a deployment or spike
    ///            without being hammered by retrying clients. Half-open probe after 30 s.
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> BuildResiliencePipeline()
    {
        var timeout = Policy.TimeoutAsync<HttpResponseMessage>(
            seconds: 5,
            timeoutStrategy: TimeoutStrategy.Optimistic);

        var retryDelays = new[] { 1, 2, 4 };
        var rng = new Random();

        var retry = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt =>
                {
                    var baseDelay = retryDelays[Math.Min(attempt - 1, retryDelays.Length - 1)];
                    var jitter = rng.NextDouble() * 0.4 - 0.2; // ±20%
                    return TimeSpan.FromSeconds(baseDelay * (1 + jitter));
                });

        var circuitBreaker = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));

        // Wrap outermost-first: circuit breaker → retry → timeout.
        // The timeout wraps each individual attempt; the circuit breaker wraps all attempts.
        return Policy.WrapAsync(circuitBreaker, retry, timeout);
    }
}
