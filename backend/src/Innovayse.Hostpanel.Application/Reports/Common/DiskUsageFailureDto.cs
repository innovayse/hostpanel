using Innovayse.Hostpanel.Application.Reports.Common.Enums;

namespace Innovayse.Hostpanel.Application.Reports.Common;

/// <summary>One server a disk usage refresh could not read.</summary>
/// <param name="ServerName">Name of the hosting server.</param>
/// <param name="Reason">Why the server gave no figures.</param>
public record DiskUsageFailureDto(
    string ServerName,
    DiskUsageFailureReason Reason);
