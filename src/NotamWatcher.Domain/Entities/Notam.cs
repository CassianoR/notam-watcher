using NotamWatcher.Domain.Enums;

namespace NotamWatcher.Domain.Entities;

public sealed class Notam
{
    public int Id { get; init; }
    public required string NotamNumber { get; init; }
    public required string IcaoLocation { get; init; }
    public required string QCode { get; init; }
    public required string Subject { get; init; }
    public required string Condition { get; init; }
    public DateTime? StartValidity { get; init; }
    public DateTime? EndValidity { get; init; }
    public required string FreeText { get; init; }
    public required string RawText { get; init; }
    public NotamSeverity Severity { get; init; }
    public NotamClassification Classification { get; init; }
    public DateTime FetchedAt { get; init; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
