using NotamWatcher.Domain.Enums;

namespace NotamWatcher.Parsing.Models;

/// <summary>
/// Structured result of parsing a raw NOTAM string.
/// All nullable fields are legitimately absent in some NOTAM formats.
/// </summary>
public sealed record ParsedNotam
{
    public required string NotamNumber { get; init; }
    public required string IcaoLocation { get; init; }

    // Q-line components
    public string? Fir { get; init; }
    public string? QCode { get; init; }
    public string? Subject { get; init; }
    public string? Condition { get; init; }

    // Validity window
    public DateTime? StartValidity { get; init; }
    public DateTime? EndValidity { get; init; }
    public bool IsPermanent { get; init; }
    public bool IsEstimated { get; init; }

    // Body
    public required string FreeText { get; init; }
    public required string RawText { get; init; }

    public NotamSeverity Severity { get; init; }
    public NotamClassification Classification { get; init; }
}
