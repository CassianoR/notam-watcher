namespace NotamWatcher.Infrastructure.Configuration;

public sealed class FaaApiOptions
{
    public const string Section = "FaaApi";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.faa.gov/notamapi/v1";
    public int FetchIntervalSeconds { get; set; } = 60;
    public int PageSize { get; set; } = 100;
}
