namespace NotamWatcher.Infrastructure.FaaApi;

public interface IFaaNotamClient
{
    /// <summary>
    /// Fetches a single page of NOTAMs for the given ICAO location codes.
    /// Returns null on a circuit-open or unrecoverable error (caller skips the cycle).
    /// </summary>
    Task<FaaNotamResponse?> FetchAsync(
        IReadOnlyCollection<string> icaoCodes,
        int pageNum = 1,
        CancellationToken ct = default);
}
