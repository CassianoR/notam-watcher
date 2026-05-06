namespace NotamWatcher.Infrastructure.Configuration;

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    public string Path { get; set; } = "notams.db";
}
