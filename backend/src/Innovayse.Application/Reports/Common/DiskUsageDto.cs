namespace Innovayse.Application.Reports.Common;

/// <summary>Full Disk Usage Summary report result.</summary>
/// <param name="Servers">Accounts grouped by the server hosting them.</param>
/// <param name="LastUpdated">When the cache was last refreshed, or null if it never has been.</param>
public record DiskUsageDto(
    IReadOnlyList<DiskUsageServerDto> Servers,
    DateTimeOffset? LastUpdated);
