using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using NotamWatcher.Api.Hubs;
using NotamWatcher.Domain.Entities;
using NotamWatcher.Infrastructure.Configuration;
using NotamWatcher.Infrastructure.FaaApi;
using NotamWatcher.Infrastructure.Persistence;
using NotamWatcher.Parsing;

namespace NotamWatcher.Api.BackgroundServices;

/// <summary>
/// Background service that polls the FAA NOTAM API on a configurable interval.
///
/// Design notes:
///   - Uses PeriodicTimer (introduced in .NET 6) rather than System.Threading.Timer.
///     PeriodicTimer fires exactly once per tick regardless of how long the work takes,
///     avoids timer drift, and supports clean cancellation via CancellationToken without
///     needing manual locking or manual disposal.
///   - Scoped services (DbContext, repositories) are resolved from a per-cycle scope.
///     BackgroundService is a singleton; creating a DI scope per cycle is the correct
///     pattern for accessing Scoped services from a singleton host.
///   - The FAA client returns null on circuit-open or unrecoverable error; the service
///     logs and skips rather than throwing, so the host stays alive.
/// </summary>
public sealed class NotamFetcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<NotamHub> _hub;
    private readonly INotamParser _parser;
    private readonly FaaApiOptions _options;
    private readonly ILogger<NotamFetcherService> _logger;

    public NotamFetcherService(
        IServiceScopeFactory scopeFactory,
        IHubContext<NotamHub> hub,
        INotamParser parser,
        IOptions<FaaApiOptions> options,
        ILogger<NotamFetcherService> logger)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
        _parser = parser;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "NotamFetcherService starting. Interval: {Interval}s",
            _options.FetchIntervalSeconds);

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(_options.FetchIntervalSeconds));

        // Run immediately on startup, then wait for subsequent ticks.
        await RunFetchCycleAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunFetchCycleAsync(stoppingToken);
        }
    }

    private async Task RunFetchCycleAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var routeRepo = scope.ServiceProvider.GetRequiredService<WatchedRouteRepository>();
        var notamRepo = scope.ServiceProvider.GetRequiredService<NotamRepository>();
        var faaClient = scope.ServiceProvider.GetRequiredService<IFaaNotamClient>();

        var routes = await routeRepo.GetAllAsync(ct);
        if (routes.Count == 0) return;

        // Collect the union of all watched ICAO codes.
        var allCodes = routes
            .SelectMany(r => System.Text.Json.JsonSerializer
                .Deserialize<string[]>(r.IcaoCodes) ?? Array.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (allCodes.Count == 0) return;

        _logger.LogDebug("Fetching NOTAMs for {Count} locations: {Codes}",
            allCodes.Count, string.Join(", ", allCodes));

        var response = await faaClient.FetchAsync(allCodes, pageNum: 1, ct);
        if (response is null)
        {
            _logger.LogWarning("Fetch cycle skipped — FAA client returned null");
            return;
        }

        int newCount = 0, updatedCount = 0;

        foreach (var item in response.Items)
        {
            var raw = item.Properties?.CoreNotamData?.Notam?.Text;
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var parseResult = _parser.Parse(raw);
            if (!parseResult.IsOk)
            {
                _logger.LogDebug("Could not parse NOTAM text: {Reason}",
                    ((Parsing.Models.ParseResult<Parsing.Models.ParsedNotam>.Fail)parseResult).Reason);
                continue;
            }

            var parsed = ((Parsing.Models.ParseResult<Parsing.Models.ParsedNotam>.Ok)parseResult).Value;

            var existing = await notamRepo.FindByNumberAsync(parsed.NotamNumber, ct);

            if (existing is null)
            {
                var notam = MapToEntity(parsed);
                await notamRepo.AddAsync(notam, ct);
                await notamRepo.SaveAsync(ct);
                await BroadcastAsync(notam, routes, "NotamNew", ct);
                newCount++;
            }
            else if (HasChanged(existing, parsed))
            {
                await notamRepo.UpdateAsync(existing, MapToEntity(parsed), ct);
                await notamRepo.SaveAsync(ct);
                await BroadcastAsync(existing, routes, "NotamUpdated", ct);
                updatedCount++;
            }
        }

        if (newCount > 0 || updatedCount > 0)
            _logger.LogInformation("Cycle complete. New: {New}, Updated: {Updated}", newCount, updatedCount);
    }

    private async Task BroadcastAsync(
        Notam notam,
        IEnumerable<WatchedRoute> routes,
        string eventName,
        CancellationToken ct)
    {
        foreach (var route in routes)
        {
            var codes = System.Text.Json.JsonSerializer
                .Deserialize<string[]>(route.IcaoCodes) ?? Array.Empty<string>();

            if (codes.Contains(notam.IcaoLocation, StringComparer.OrdinalIgnoreCase))
            {
                await _hub.Clients.Group(route.RouteKey)
                    .SendAsync(eventName, notam, ct);
            }
        }
    }

    private static Notam MapToEntity(Parsing.Models.ParsedNotam parsed) => new()
    {
        NotamNumber = parsed.NotamNumber,
        IcaoLocation = parsed.IcaoLocation,
        QCode = parsed.QCode ?? string.Empty,
        Subject = parsed.Subject ?? string.Empty,
        Condition = parsed.Condition ?? string.Empty,
        StartValidity = parsed.StartValidity,
        EndValidity = parsed.EndValidity,
        FreeText = parsed.FreeText,
        RawText = parsed.RawText,
        Severity = parsed.Severity,
        Classification = parsed.Classification,
        FetchedAt = DateTime.UtcNow
    };

    private static bool HasChanged(Notam existing, Parsing.Models.ParsedNotam parsed) =>
        existing.FreeText != parsed.FreeText ||
        existing.EndValidity != parsed.EndValidity;
}
