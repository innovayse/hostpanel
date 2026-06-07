namespace Innovayse.Application.Reports.Interfaces;

using Innovayse.Application.Reports.DTOs;

/// <summary>Service for retrieving and caching disk/bandwidth usage from hosting servers.</summary>
public interface IDiskUsageService
{
    /// <summary>Returns cached disk usage stats grouped by server.</summary>
    Task<DiskUsageDto> GetReportAsync(CancellationToken ct);

    /// <summary>Fetches fresh stats from all servers and updates the cache.</summary>
    Task<DiskUsageDto> UpdateNowAsync(CancellationToken ct);
}
