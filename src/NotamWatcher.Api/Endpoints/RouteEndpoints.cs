using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NotamWatcher.Domain.Entities;
using NotamWatcher.Infrastructure.Persistence;

namespace NotamWatcher.Api.Endpoints;

public static class RouteEndpoints
{
    public static IEndpointRouteBuilder MapRouteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/routes").WithTags("Routes");

        group.MapGet("/", GetAllRoutes)
            .WithName("GetRoutes")
            .WithSummary("List all watched routes");

        group.MapPost("/", CreateRoute)
            .WithName("CreateRoute")
            .WithSummary("Register a new watched route");

        group.MapDelete("/{routeKey}", DeleteRoute)
            .WithName("DeleteRoute")
            .WithSummary("Remove a watched route");

        return app;
    }

    private static async Task<IResult> GetAllRoutes(
        WatchedRouteRepository repo,
        CancellationToken ct)
    {
        var routes = await repo.GetAllAsync(ct);
        return Results.Ok(routes.Select(r => new
        {
            r.Id,
            r.RouteKey,
            IcaoCodes = JsonSerializer.Deserialize<string[]>(r.IcaoCodes),
            r.CreatedAt
        }));
    }

    private static async Task<IResult> CreateRoute(
        [FromBody] CreateRouteRequest req,
        WatchedRouteRepository repo,
        CancellationToken ct)
    {
        if (req.IcaoCodes is not { Length: >= 1 })
            return Results.BadRequest("At least one ICAO code is required.");

        var normalized = req.IcaoCodes
            .Select(c => c.Trim().ToUpperInvariant())
            .Where(c => c.Length == 4)
            .Distinct()
            .OrderBy(c => c)
            .ToArray();

        if (normalized.Length == 0)
            return Results.BadRequest("No valid 4-character ICAO codes provided.");

        var routeKey = string.Join("-", normalized);
        var route = new WatchedRoute
        {
            RouteKey = routeKey,
            IcaoCodes = JsonSerializer.Serialize(normalized)
        };

        var result = await repo.UpsertAsync(route, ct);
        return Results.Created($"/api/routes/{routeKey}", new
        {
            result.Id,
            result.RouteKey,
            IcaoCodes = normalized,
            result.CreatedAt
        });
    }

    private static async Task<IResult> DeleteRoute(
        string routeKey,
        WatchedRouteRepository repo,
        CancellationToken ct)
    {
        await repo.DeleteAsync(routeKey, ct);
        return Results.NoContent();
    }

    private record CreateRouteRequest(string[] IcaoCodes);
}
