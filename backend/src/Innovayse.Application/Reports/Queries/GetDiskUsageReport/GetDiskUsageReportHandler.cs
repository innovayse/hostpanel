namespace Innovayse.Application.Reports.Queries.GetDiskUsageReport;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="GetDiskUsageReportQuery"/>.</summary>
/// <param name="diskService">Disk and bandwidth usage cache over the hosting servers.</param>
public sealed class GetDiskUsageReportHandler(IDiskUsageService diskService)
{
    /// <summary>
    /// Returns the cached disk and bandwidth figures. Reads the cache only, so an admin opening
    /// the report never waits on every hosting server; refreshing is a separate command.
    /// </summary>
    /// <param name="query">The query. Carries no filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Accounts grouped by server, with the time the cache was last refreshed.</returns>
    public Task<DiskUsageDto> HandleAsync(GetDiskUsageReportQuery query, CancellationToken ct)
        => diskService.GetReportAsync(ct);
}
