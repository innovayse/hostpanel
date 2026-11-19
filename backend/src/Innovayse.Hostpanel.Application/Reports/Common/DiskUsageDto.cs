namespace Innovayse.Hostpanel.Application.Reports.Common;

/// <summary>Full Disk Usage Summary report result.</summary>
/// <param name="Servers">Accounts grouped by the server hosting them.</param>
/// <param name="LastUpdated">When the cache was last refreshed, or null if it never has been.</param>
/// <param name="Failures">Servers the last refresh could not read; empty when the report is only read from the cache.</param>
public record DiskUsageDto(
    IReadOnlyList<DiskUsageServerDto> Servers,
    DateTimeOffset? LastUpdated,
    IReadOnlyList<DiskUsageFailureDto> Failures);
