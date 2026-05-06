using Microsoft.EntityFrameworkCore;
using NotamWatcher.Domain.Entities;

namespace NotamWatcher.Infrastructure.Persistence;

public sealed class WatchedRouteRepository
{
    private readonly AppDbContext _db;

    public WatchedRouteRepository(AppDbContext db) => _db = db;

    public Task<List<WatchedRoute>> GetAllAsync(CancellationToken ct = default) =>
        _db.WatchedRoutes.AsNoTracking().ToListAsync(ct);

    public Task<WatchedRoute?> FindByKeyAsync(string routeKey, CancellationToken ct = default) =>
        _db.WatchedRoutes.AsNoTracking().FirstOrDefaultAsync(r => r.RouteKey == routeKey, ct);

    public async Task<WatchedRoute> UpsertAsync(WatchedRoute route, CancellationToken ct = default)
    {
        var existing = await _db.WatchedRoutes
            .FirstOrDefaultAsync(r => r.RouteKey == route.RouteKey, ct);

        if (existing is not null)
            return existing;

        await _db.WatchedRoutes.AddAsync(route, ct);
        await _db.SaveChangesAsync(ct);
        return route;
    }

    public async Task DeleteAsync(string routeKey, CancellationToken ct = default)
    {
        var route = await _db.WatchedRoutes
            .FirstOrDefaultAsync(r => r.RouteKey == routeKey, ct);

        if (route is not null)
        {
            _db.WatchedRoutes.Remove(route);
            await _db.SaveChangesAsync(ct);
        }
    }
}
