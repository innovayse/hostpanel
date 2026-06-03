namespace Innovayse.Application.Reports.Queries.InvoicesReport;

/// <summary>Query for the Invoices report with filters.</summary>
public record InvoicesReportQuery(
    string? Status = null,
    DateOnly? CreatedFrom = null,
    DateOnly? CreatedTo = null,
    DateOnly? DueFrom = null,
    DateOnly? DueTo = null,
    DateOnly? PaidFrom = null,
    DateOnly? PaidTo = null,
    int Page = 1,
    int PageSize = 50);
