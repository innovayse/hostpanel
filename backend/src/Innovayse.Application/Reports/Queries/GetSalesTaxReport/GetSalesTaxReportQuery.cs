namespace Innovayse.Application.Reports.Queries.GetSalesTaxReport;

/// <summary>Query for the Sales Tax Liability report.</summary>
/// <param name="From">Earliest date to include, or null for no lower bound.</param>
/// <param name="To">Latest date to include, or null for no upper bound.</param>
public record GetSalesTaxReportQuery(DateOnly? From = null, DateOnly? To = null);
