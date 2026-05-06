using Microsoft.EntityFrameworkCore;
using NotamWatcher.Domain.Entities;

namespace NotamWatcher.Infrastructure.Persistence;

public sealed class NotamRepository
{
    private readonly AppDbContext _db;

    public NotamRepository(AppDbContext db) => _db = db;

    /// <summary>Returns NOTAMs for any of the given ICAO codes, ordered newest-first.</summary>
    public Task<List<Notam>> GetByLocationsAsync(
        IReadOnlyCollection<string> icaoCodes,
        CancellationToken ct = default) =>
        _db.Notams
            .AsNoTracking()
            .Where(n => icaoCodes.Contains(n.IcaoLocation))
            .OrderByDescending(n => n.FetchedAt)
            .ToListAsync(ct);

    /// <summary>Returns a single NOTAM by its unique number, or null.</summary>
    public Task<Notam?> FindByNumberAsync(string notamNumber, CancellationToken ct = default) =>
        _db.Notams
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.NotamNumber == notamNumber, ct);

    /// <summary>Inserts a new NOTAM. Caller is responsible for calling SaveChangesAsync.</summary>
    public async Task AddAsync(Notam notam, CancellationToken ct = default) =>
        await _db.Notams.AddAsync(notam, ct);

    /// <summary>
    /// Marks an existing tracked NOTAM's UpdatedAt and FreeText as changed.
    /// Uses explicit update rather than change-tracking to avoid loading the full entity.
    /// </summary>
    public Task UpdateAsync(Notam existing, Notam incoming, CancellationToken ct = default)
    {
        _db.Notams.Attach(existing);
        existing.UpdatedAt = DateTime.UtcNow;
        _db.Entry(existing).Property(nameof(Notam.UpdatedAt)).IsModified = true;
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
