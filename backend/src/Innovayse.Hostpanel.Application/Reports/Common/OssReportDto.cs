namespace Innovayse.Hostpanel.Application.Reports.Common;

/// <summary>One Stop Shop (OSS) report result for one calendar quarter.</summary>
/// <param name="PeriodStart">The first day of the quarter (UTC), <c>yyyy-MM-dd</c> on the wire.</param>
/// <param name="PeriodEnd">The last day of the quarter (UTC), inclusive.</param>
/// <param name="Rows">One row per member state, VAT rate and invoice currency.</param>
public sealed record OssReportDto(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    IReadOnlyList<OssReportRowDto> Rows);
