namespace Innovayse.Application.Reports.Queries.GetVatMoss;

/// <summary>Query for the VAT MOSS settlement report.</summary>
/// <param name="Year">Calendar year, or null for the current UTC year.</param>
/// <param name="Quarter">Calendar quarter 1-4, or null for the quarter the current UTC month falls in.</param>
public record GetVatMossQuery(int? Year = null, int? Quarter = null);
