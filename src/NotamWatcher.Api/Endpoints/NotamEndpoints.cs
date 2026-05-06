using Microsoft.AspNetCore.Mvc;
using NotamWatcher.Infrastructure.Persistence;

namespace NotamWatcher.Api.Endpoints;

public static class NotamEndpoints
{
    public static IEndpointRouteBuilder MapNotamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notams").WithTags("NOTAMs");

        group.MapGet("/", GetNotamsForLocations)
            .WithName("GetNotams")
            .WithSummary("Get current NOTAMs for one or more ICAO locations");

        return app;
    }

    private static async Task<IResult> GetNotamsForLocations(
        [FromQuery] string locations,
        NotamRepository repo,
        CancellationToken ct)
    {
        var codes = locations
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.ToUpperInvariant())
            .Where(c => c.Length == 4)
            .ToArray();

        if (codes.Length == 0)
            return Results.BadRequest("Provide at least one 4-character ICAO code via ?locations=KJFK,KLAX");

        var notams = await repo.GetByLocationsAsync(codes, ct);
        return Results.Ok(notams);
    }
}
