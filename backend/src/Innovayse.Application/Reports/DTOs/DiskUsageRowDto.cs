namespace Innovayse.Application.Reports.DTOs;

/// <summary>One row in the Disk Usage Summary report.</summary>
/// <param name="ClientName">Client the hosting account belongs to.</param>
/// <param name="Domain">Primary domain of the hosting account.</param>
/// <param name="DiskUsage">Disk used, already formatted for display.</param>
/// <param name="DiskLimit">Disk quota, already formatted for display.</param>
/// <param name="DiskPercent">Disk used as a percentage of the quota.</param>
/// <param name="BwUsage">Bandwidth used, already formatted for display.</param>
/// <param name="BwLimit">Bandwidth quota, already formatted for display.</param>
/// <param name="BwPercent">Bandwidth used as a percentage of the quota.</param>
public record DiskUsageRowDto(
    string ClientName,
    string Domain,
    string DiskUsage,
    string DiskLimit,
    int DiskPercent,
    string BwUsage,
    string BwLimit,
    int BwPercent);
