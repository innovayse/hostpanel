namespace Innovayse.Hostpanel.Application.Reports.Queries.GetOssReport;

/// <summary>Query for the OSS report.</summary>
/// <param name="Year">Calendar year, or null for the current UTC year.</param>
/// <param name="Quarter">Calendar quarter 1-4, or null for the quarter the current UTC month falls in.</param>
public sealed record GetOssReportQuery(int? Year = null, int? Quarter = null);
