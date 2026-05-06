using System.Text.RegularExpressions;
using NotamWatcher.Domain.Enums;
using NotamWatcher.Parsing.Models;

namespace NotamWatcher.Parsing;

/// <summary>
/// Parses raw NOTAM strings (ICAO Annex 15 format, FAA variant) into <see cref="ParsedNotam"/>.
///
/// A NOTAM has the structure:
///   NOTAM NUMBER) SERIES/YEAR/TYPE/ICAO
///   Q) FIR/QCODE/TRAFFIC/PURPOSE/SCOPE/LOWER/UPPER/COORDINATES
///   A) ICAO_LOCATION  B) START_VALIDITY  C) END_VALIDITY
///   E) FREE TEXT
///
/// Real-world samples deviate: missing Q lines, non-standard date tokens, multi-line E fields.
/// The extractor pipeline handles each field independently so one bad field doesn't kill the parse.
/// </summary>
public sealed class NotamParser : INotamParser
{
    // e.g. "A0014/25 NOTAMN" or "!FDC 5/0001 KJFK A0014/25 NOTAMN"
    // Simplified: just look for the Series/Year/Type token anywhere in the text.
    private static readonly Regex NotamNumberRx = new(
        @"([A-Z]\d{4}/\d{2})\s+NOTAM([NRC])",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // Q) FIR/QCODE/TRAFFIC/PURPOSE/SCOPE/LOWER/UPPER/COORD
    private static readonly Regex QLineRx = new(
        @"Q\)\s*([A-Z]{4})/([A-Z]{2,6})/([A-Z!\/]*)/([A-Z!\/]*)/([A-Z!\/]*)/(\d{3})/(\d{3})/(\S+)",
        RegexOptions.Compiled);

    // A) ICAO  B) YYMMDDhhmm  C) YYMMDDhhmm|PERM|YYMMDDhhmmEST
    private static readonly Regex AFieldRx = new(@"A\)\s*([A-Z]{4})", RegexOptions.Compiled);
    private static readonly Regex BFieldRx = new(@"B\)\s*(\d{10})", RegexOptions.Compiled);
    private static readonly Regex CFieldRx = new(
        @"C\)\s*(\d{10}(?:EST)?|PERM)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // E) free text (everything after E) up to next field marker or end)
    private static readonly Regex EFieldRx = new(
        @"E\)\s*([\s\S]+?)(?=\n[A-Z]\)|$)",
        RegexOptions.Compiled);

    // Severity keyword sets — order matters (most severe checked first)
    private static readonly string[] CriticalKeywords =
        new[] { "RWY CLSD", "TWY CLSD", "AERODROME CLSD", "AD CLSD", "ILS U/S", "VOR U/S", "NDB U/S", " U/S" };
    private static readonly string[] WarningKeywords =
        new[] { "OBSTACLE", "CRANE", "LASER", "UAS", "DRONE", "RESTRICTED", "PROHIBITED", "TFR" };
    private static readonly string[] CautionKeywords =
        new[] { "WORK IN PROGRESS", "WIP", "TAXIWAY", "APRON", "REDUCED" };

    public ParseResult<ParsedNotam> Parse(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return new ParseResult<ParsedNotam>.Fail("Input is null or empty");

        var normalized = NormalizeLineEndings(rawText.Trim());

        var numberResult = ExtractNotamNumber(normalized);
        if (numberResult is ParseResult<(string Number, string Type)>.Fail nf)
            return new ParseResult<ParsedNotam>.Fail($"Cannot identify NOTAM number: {nf.Reason}");
        var (notamNumber, notamType) = ((ParseResult<(string, string)>.Ok)numberResult).Value;

        var location = ExtractAField(normalized);
        if (location is null)
            return new ParseResult<ParsedNotam>.Fail("A) field (ICAO location) not found");

        var (qCode, subject, condition, fir) = ExtractQLine(normalized);
        var (startValidity, endValidity, isPermanent, isEstimated) = ExtractValidity(normalized);
        var freeText = ExtractEField(normalized);
        var classification = DeriveClassification(notamType, qCode);
        var severity = DeriveSeverity(qCode, freeText, classification);

        return new ParseResult<ParsedNotam>.Ok(new ParsedNotam
        {
            NotamNumber = notamNumber,
            IcaoLocation = location,
            Fir = fir,
            QCode = qCode,
            Subject = subject,
            Condition = condition,
            StartValidity = startValidity,
            EndValidity = endValidity,
            IsPermanent = isPermanent,
            IsEstimated = isEstimated,
            FreeText = freeText ?? string.Empty,
            RawText = rawText,
            Severity = severity,
            Classification = classification
        });
    }

    // ── Private extractors ────────────────────────────────────────────────────

    private static ParseResult<(string Number, string Type)> ExtractNotamNumber(string text)
    {
        var m = NotamNumberRx.Match(text);
        if (!m.Success)
            return new ParseResult<(string, string)>.Fail("No NOTAM[NRC] identifier found");
        return new ParseResult<(string, string)>.Ok((m.Groups[1].Value, m.Groups[2].Value));
    }

    private static string? ExtractAField(string text)
    {
        var m = AFieldRx.Match(text);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static (string? QCode, string? Subject, string? Condition, string? Fir)
        ExtractQLine(string text)
    {
        var m = QLineRx.Match(text);
        if (!m.Success) return (null, null, null, null);

        var fir = m.Groups[1].Value;
        var rawQ = m.Groups[2].Value; // e.g. "QMRLC"
        var (subject, condition) = DecodeQCode(rawQ);
        return (rawQ, subject, condition, fir);
    }

    private static (DateTime? Start, DateTime? End, bool IsPermanent, bool IsEstimated)
        ExtractValidity(string text)
    {
        var bm = BFieldRx.Match(text);
        var cm = CFieldRx.Match(text);

        DateTime? start = bm.Success ? ParseNotamDate(bm.Groups[1].Value) : null;

        bool isPermanent = false;
        bool isEstimated = false;
        DateTime? end = null;

        if (cm.Success)
        {
            var cVal = cm.Groups[1].Value.ToUpperInvariant();
            if (cVal == "PERM")
            {
                isPermanent = true;
            }
            else
            {
                isEstimated = cVal.EndsWith("EST");
                var datePart = cVal.Replace("EST", "");
                end = ParseNotamDate(datePart);
            }
        }

        return (start, end, isPermanent, isEstimated);
    }

    private static string? ExtractEField(string text)
    {
        var m = EFieldRx.Match(text);
        if (!m.Success) return null;
        return m.Groups[1].Value.Trim();
    }

    // ── Date parsing ──────────────────────────────────────────────────────────

    private static DateTime? ParseNotamDate(string token)
    {
        // Standard: YYMMDDhhmm  (e.g. "2501150800")
        if (token.Length == 10 &&
            int.TryParse(token[..2], out var yy) &&
            int.TryParse(token[2..4], out var mm) &&
            int.TryParse(token[4..6], out var dd) &&
            int.TryParse(token[6..8], out var hh) &&
            int.TryParse(token[8..10], out var min))
        {
            var year = 2000 + yy;
            if (mm is >= 1 and <= 12 && dd is >= 1 and <= 31 && hh is >= 0 and <= 23 && min is >= 0 and <= 59)
            {
                try { return new DateTime(year, mm, dd, hh, min, 0, DateTimeKind.Utc); }
                catch (ArgumentOutOfRangeException) { return null; }
            }
        }

        // Fallback: try standard DateTime parse
        if (DateTime.TryParse(token, out var dt))
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);

        return null;
    }

    // ── Q-code decoder ────────────────────────────────────────────────────────

    // ICAO Q-codes: Q + 2-char subject + 2-char condition
    // e.g. QMRLC → subject=MR (runway), condition=LC (closed)
    private static (string? Subject, string? Condition) DecodeQCode(string rawQ)
    {
        if (rawQ.Length < 5 || rawQ[0] != 'Q')
            return (null, null);

        var subjectCode = rawQ[1..3];
        var conditionCode = rawQ[3..5];

        var subject = subjectCode switch
        {
            "MR" => "Runway",
            "MT" => "Taxiway",
            "MA" => "Movement area",
            "MK" => "Parking area",
            "IL" => "ILS",
            "IV" => "VOR",
            "IN" => "NDB",
            "IC" => "COM facility",
            "LT" => "Lighting",
            "OB" => "Obstacle",
            "XX" => "Airspace",
            "RT" => "Route",
            "SP" => "Special activity",
            "AS" => "Airspace",
            "WU" => "UAS",
            _ => subjectCode
        };

        var condition = conditionCode switch
        {
            "LC" => "Closed",
            "LO" => "Operational",
            "XX" => "Unserviceable",
            "US" => "Unserviceable",
            "RH" => "Hours of operation changed",
            "LA" => "Available",
            "CH" => "Changed",
            "NW" => "New",
            "LI" => "Commissioned",
            _ => conditionCode
        };

        return (subject, condition);
    }

    // ── Classification & severity ─────────────────────────────────────────────

    private static NotamClassification DeriveClassification(string notamType, string? qCode)
    {
        if (qCode is null) return NotamClassification.Unknown;
        if (notamType == "C") return NotamClassification.Checklist;

        return qCode[1..3] switch
        {
            "MR" or "MT" or "MA" or "MK" or "LT" => NotamClassification.Aerodrome,
            "IL" or "IV" or "IN" or "IC" => NotamClassification.Aerodrome,
            "OB" => NotamClassification.Warning,
            "XX" or "RT" or "AS" or "SP" => NotamClassification.Enroute,
            _ => NotamClassification.Unknown
        };
    }

    private static NotamSeverity DeriveSeverity(string? qCode, string? freeText, NotamClassification classification)
    {
        // Start with Q-code condition — structural signal is more reliable than keyword matching.
        var severity = NotamSeverity.Advisory;

        if (qCode?.Length >= 5)
        {
            var condCode = qCode[3..5];
            // XX is overloaded in FAA Q-codes (used for both "unserviceable" and "various").
            // Keyword scan handles the Critical escalation for specific U/S cases.
            severity = condCode switch
            {
                "LC" or "US"  => NotamSeverity.Critical,
                "XX"          => NotamSeverity.Warning,
                "CH" or "RH"  => NotamSeverity.Caution,
                _             => NotamSeverity.Advisory
            };
        }

        if (classification == NotamClassification.Warning && severity < NotamSeverity.Warning)
            severity = NotamSeverity.Warning;

        // Keyword scan can only escalate, never demote — WIP/TAXIWAY in a closed-runway NOTAM
        // shouldn't override the Q-code-derived Critical.
        var haystack = $"{qCode} {freeText}".ToUpperInvariant();

        foreach (var kw in CriticalKeywords)
            if (haystack.Contains(kw)) return NotamSeverity.Critical;

        if (severity < NotamSeverity.Warning)
        {
            foreach (var kw in WarningKeywords)
                if (haystack.Contains(kw)) { severity = NotamSeverity.Warning; break; }
        }

        if (severity < NotamSeverity.Caution)
        {
            foreach (var kw in CautionKeywords)
                if (haystack.Contains(kw)) { severity = NotamSeverity.Caution; break; }
        }

        return severity;
    }

    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n');
}
