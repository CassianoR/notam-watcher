using System.Text.Json.Serialization;

namespace NotamWatcher.Infrastructure.FaaApi;

/// <summary>
/// Top-level envelope from GET /notamapi/v1/notams.
/// The FAA API paginates via pageSize / pageNum query params.
/// </summary>
public sealed record FaaNotamResponse(
    [property: JsonPropertyName("pageSize")]   int PageSize,
    [property: JsonPropertyName("pageNum")]    int PageNum,
    [property: JsonPropertyName("totalCount")] int TotalCount,
    [property: JsonPropertyName("totalPages")] int TotalPages,
    [property: JsonPropertyName("items")]      List<FaaNotamItem> Items
);

public sealed record FaaNotamItem(
    [property: JsonPropertyName("type")]       string Type,
    [property: JsonPropertyName("properties")] FaaNotamProperties Properties
);

public sealed record FaaNotamProperties(
    [property: JsonPropertyName("coreNOTAMData")] FaaCoreNotamData CoreNotamData
);

public sealed record FaaCoreNotamData(
    [property: JsonPropertyName("notam")] FaaNotam Notam
);

public sealed record FaaNotam(
    [property: JsonPropertyName("id")]             string Id,
    [property: JsonPropertyName("number")]         string Number,
    [property: JsonPropertyName("type")]           string Type,
    [property: JsonPropertyName("issued")]         string Issued,
    [property: JsonPropertyName("affectedFIR")]    string? AffectedFir,
    [property: JsonPropertyName("selectionCode")]  string? SelectionCode,
    [property: JsonPropertyName("traffic")]        string? Traffic,
    [property: JsonPropertyName("purpose")]        string? Purpose,
    [property: JsonPropertyName("scope")]          string? Scope,
    [property: JsonPropertyName("minimumFL")]      string? MinimumFl,
    [property: JsonPropertyName("maximumFL")]      string? MaximumFl,
    [property: JsonPropertyName("location")]       string Location,
    [property: JsonPropertyName("effectiveStart")] string? EffectiveStart,
    [property: JsonPropertyName("effectiveEnd")]   string? EffectiveEnd,
    [property: JsonPropertyName("text")]           string Text,
    [property: JsonPropertyName("classification")] string? Classification,
    [property: JsonPropertyName("schedule")]       string? Schedule
);
