namespace Innovayse.Application.Reports.Queries.TransactionsReport;

/// <summary>Query for the Transactions report with filters.</summary>
public record TransactionsReportQuery(
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    string? PaymentMethod = null,
    int Page = 1,
    int PageSize = 50);
