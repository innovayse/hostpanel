namespace Innovayse.Application.Reports.Common;

/// <summary>One server group in the Disk Usage Summary report.</summary>
/// <param name="ServerName">Name of the hosting server the accounts live on.</param>
/// <param name="Rows">Accounts hosted on that server.</param>
public record DiskUsageServerDto(
    string ServerName,
    IReadOnlyList<DiskUsageRowDto> Rows);
