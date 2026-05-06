namespace NotamWatcher.Domain.Entities;

public sealed class WatchedRoute
{
    public int Id { get; init; }
    public required string RouteKey { get; init; }       // "KJFK-KLAX-KORD"
    public required string IcaoCodes { get; init; }      // JSON array stored as string
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
