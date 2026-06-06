namespace Innovayse.Application.Reports.DTOs;

/// <summary>One row in the Disk Usage Summary report.</summary>
public record DiskUsageRowDto(
    string ClientName,
    string Domain,
    string DiskUsage,
    string DiskLimit,
    int DiskPercent,
    string BwUsage,
    string BwLimit,
    int BwPercent);

/// <summary>One server group in the Disk Usage Summary report.</summary>
public record DiskUsageServerDto(
    string ServerName,
    IReadOnlyList<DiskUsageRowDto> Rows);

/// <summary>Full Disk Usage Summary report result.</summary>
public record DiskUsageDto(
    IReadOnlyList<DiskUsageServerDto> Servers,
    DateTimeOffset? LastUpdated);
