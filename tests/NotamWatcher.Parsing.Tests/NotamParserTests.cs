using NotamWatcher.Domain.Enums;
using NotamWatcher.Parsing;
using NotamWatcher.Parsing.Models;

namespace NotamWatcher.Parsing.Tests;

/// <summary>
/// Unit tests for <see cref="NotamParser"/>. Exercises well-formed NOTAMs,
/// malformed input, edge cases, and real fixture samples.
/// All tests are offline — no network, no database.
/// </summary>
public sealed class NotamParserTests
{
    private readonly INotamParser _parser = new NotamParser();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string ReadFixture(string fileName) =>
        File.ReadAllText(Path.Combine("fixtures", fileName));

    private ParsedNotam ParseOk(string raw)
    {
        var result = _parser.Parse(raw);
        Assert.True(result.IsOk, result is ParseResult<ParsedNotam>.Fail f ? f.Reason : "");
        return ((ParseResult<ParsedNotam>.Ok)result).Value;
    }

    // ── Well-formed NOTAMs ────────────────────────────────────────────────────

    [Fact]
    public void Parse_WellFormedNotam_ReturnsOkResult()
    {
        var raw = ReadFixture("notam_01_rwy_closed.txt");
        var result = _parser.Parse(raw);
        Assert.True(result.IsOk);
    }

    [Fact]
    public void Parse_WellFormedNotam_ExtractsNotamNumber()
    {
        var notam = ParseOk(ReadFixture("notam_01_rwy_closed.txt"));
        Assert.Equal("A0014/25", notam.NotamNumber);
    }

    [Fact]
    public void Parse_WellFormedNotam_ExtractsIcaoLocation()
    {
        var notam = ParseOk(ReadFixture("notam_01_rwy_closed.txt"));
        Assert.Equal("KJFK", notam.IcaoLocation);
    }

    [Fact]
    public void Parse_WellFormedNotam_ExtractsStartValidity()
    {
        var notam = ParseOk(ReadFixture("notam_01_rwy_closed.txt"));
        Assert.Equal(new DateTime(2025, 2, 1, 6, 0, 0, DateTimeKind.Utc), notam.StartValidity);
    }

    [Fact]
    public void Parse_WellFormedNotam_ExtractsEndValidity()
    {
        var notam = ParseOk(ReadFixture("notam_01_rwy_closed.txt"));
        Assert.Equal(new DateTime(2025, 2, 1, 9, 0, 0, DateTimeKind.Utc), notam.EndValidity);
    }

    [Fact]
    public void Parse_WellFormedNotam_ExtractsFreeText()
    {
        var notam = ParseOk(ReadFixture("notam_01_rwy_closed.txt"));
        Assert.Contains("RWY 04L/22R CLSD", notam.FreeText);
    }

    [Fact]
    public void Parse_RunwayClosed_SeverityIsCritical()
    {
        var notam = ParseOk(ReadFixture("notam_01_rwy_closed.txt"));
        Assert.Equal(NotamSeverity.Critical, notam.Severity);
    }

    [Fact]
    public void Parse_IlsUnserviceable_SeverityIsCritical()
    {
        var notam = ParseOk(ReadFixture("notam_02_ils_unserviceable.txt"));
        Assert.Equal(NotamSeverity.Critical, notam.Severity);
    }

    [Fact]
    public void Parse_Obstacle_SeverityIsWarning()
    {
        var notam = ParseOk(ReadFixture("notam_03_obstacle.txt"));
        Assert.Equal(NotamSeverity.Warning, notam.Severity);
    }

    [Fact]
    public void Parse_Tfr_SeverityIsWarning()
    {
        var notam = ParseOk(ReadFixture("notam_04_tfr.txt"));
        Assert.Equal(NotamSeverity.Warning, notam.Severity);
    }

    [Fact]
    public void Parse_PermanentNotam_SetsPermanentFlag()
    {
        var notam = ParseOk(ReadFixture("notam_03_obstacle.txt"));
        Assert.True(notam.IsPermanent);
        Assert.Null(notam.EndValidity);
    }

    [Fact]
    public void Parse_EstimatedEndValidity_SetsIsEstimatedFlag()
    {
        var notam = ParseOk(ReadFixture("notam_05_taxiway_wip.txt"));
        Assert.True(notam.IsEstimated);
        Assert.NotNull(notam.EndValidity);
    }

    [Fact]
    public void Parse_TaxiwayWip_ClassificationIsAerodrome()
    {
        var notam = ParseOk(ReadFixture("notam_05_taxiway_wip.txt"));
        Assert.Equal(NotamClassification.Aerodrome, notam.Classification);
    }

    [Fact]
    public void Parse_QCodePresent_ExtractsSubjectAndCondition()
    {
        var notam = ParseOk(ReadFixture("notam_01_rwy_closed.txt"));
        Assert.Equal("Runway", notam.Subject);
        Assert.Equal("Closed", notam.Condition);
    }

    [Fact]
    public void Parse_QCodePresent_ExtractsFir()
    {
        var notam = ParseOk(ReadFixture("notam_01_rwy_closed.txt"));
        Assert.Equal("KZNY", notam.Fir);
    }

    [Fact]
    public void Parse_RawTextPreserved()
    {
        var raw = ReadFixture("notam_01_rwy_closed.txt");
        var notam = ParseOk(raw);
        Assert.Equal(raw, notam.RawText);
    }

    // ── Malformed input ───────────────────────────────────────────────────────

    [Fact]
    public void Parse_NullInput_ReturnsFail()
    {
        var result = _parser.Parse(null!);
        Assert.False(result.IsOk);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsFail()
    {
        var result = _parser.Parse(string.Empty);
        Assert.False(result.IsOk);
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsFail()
    {
        var result = _parser.Parse("   \n\t  ");
        Assert.False(result.IsOk);
    }

    [Fact]
    public void Parse_NoNotamIdentifier_ReturnsFail()
    {
        var result = _parser.Parse("This is just some random aviation text with no NOTAM format.");
        Assert.False(result.IsOk);
    }

    [Fact]
    public void Parse_MissingAField_ReturnsFail()
    {
        // Valid number and type, but no A) location field
        var raw = "!FDC 5/0001 KJFK A0001/25 NOTAMN\nQ) KZNY/QMRLC/IV/NBO/A/000/999/4037N07378W005\nB) 2501010000 C) 2501010100\nE) SOME TEXT";
        var result = _parser.Parse(raw);
        Assert.False(result.IsOk);
    }

    [Fact]
    public void Parse_MissingQLine_StillParsesOk()
    {
        // Q line is optional in some FDC NOTAMs
        var raw = "!FDC 5/0002 KJFK A0002/25 NOTAMN\nA) KJFK B) 2501010000 C) 2501010100\nE) AD CLSD.";
        var notam = ParseOk(raw);
        Assert.Null(notam.QCode);
        Assert.Equal("KJFK", notam.IcaoLocation);
    }

    [Fact]
    public void Parse_MissingBAndCFields_ValidityIsNull()
    {
        var raw = "!FDC 5/0003 KJFK A0003/25 NOTAMN\nA) KJFK\nE) BIRD ACTIVITY VICINITY OF AERODROME.";
        var notam = ParseOk(raw);
        Assert.Null(notam.StartValidity);
        Assert.Null(notam.EndValidity);
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_CRLFLineEndings_ParsedCorrectly()
    {
        var raw = "!FDC 5/0004 KLAX A0004/25 NOTAMN\r\nA) KLAX B) 2503010900 C) 2503011200\r\nE) APRON CLSD.";
        var notam = ParseOk(raw);
        Assert.Equal("KLAX", notam.IcaoLocation);
    }

    [Fact]
    public void Parse_MultilineEField_CapturedInFull()
    {
        var raw = ReadFixture("notam_04_tfr.txt");
        var notam = ParseOk(raw);
        Assert.Contains("PURSUANT TO 14 CFR", notam.FreeText);
        Assert.Contains("30NM RADIUS", notam.FreeText);
    }

    [Fact]
    public void Parse_InvalidDateToken_GracefullyReturnsNullValidity()
    {
        // Leap year overflow: Feb 30 does not exist
        var raw = "!FDC 5/0005 KJFK A0005/25 NOTAMN\nA) KJFK B) 2502300000 C) 2502302359\nE) TEST.";
        var notam = ParseOk(raw);
        Assert.Null(notam.StartValidity);
        Assert.Null(notam.EndValidity);
    }

    [Fact]
    public void Parse_IlsNotam_ClassificationIsAerodrome()
    {
        var notam = ParseOk(ReadFixture("notam_02_ils_unserviceable.txt"));
        Assert.Equal(NotamClassification.Aerodrome, notam.Classification);
    }

    [Fact]
    public void Parse_ParseResult_MapTransformsValue()
    {
        var raw = ReadFixture("notam_01_rwy_closed.txt");
        var result = _parser.Parse(raw);
        var mapped = result.Map(n => n.IcaoLocation);
        Assert.True(mapped.IsOk);
        Assert.Equal("KJFK", mapped.ValueOrDefault);
    }

    [Fact]
    public void Parse_FailResult_MapPreservesFailure()
    {
        var result = _parser.Parse(string.Empty);
        var mapped = result.Map(n => n.IcaoLocation);
        Assert.False(mapped.IsOk);
    }
}
