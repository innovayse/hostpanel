using Innovayse.Hostpanel.Application.Reports.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>Service for retrieving and caching disk/bandwidth usage from hosting servers.</summary>
public interface IDiskUsageService
{
    /// <summary>Returns cached disk usage stats grouped by server.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The last stored usage figures per server and account; never contacts a server.</returns>
    public Task<DiskUsageDto> GetReportAsync(CancellationToken ct);

    /// <summary>Fetches fresh stats from all servers and updates the cache.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The usage figures just fetched, the same shape the cached report has.</returns>
    public Task<DiskUsageDto> UpdateNowAsync(CancellationToken ct);
}
