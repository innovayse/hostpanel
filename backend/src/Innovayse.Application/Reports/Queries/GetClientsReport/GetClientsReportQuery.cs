namespace Innovayse.Application.Reports.Queries.GetClientsReport;

/// <summary>Query for the Clients report with filters and paging.</summary>
/// <param name="Status">Client status to keep, or null for every status.</param>
/// <param name="Country">Country to keep, or null for every country.</param>
/// <param name="CreatedFrom">Earliest signup date to include.</param>
/// <param name="CreatedTo">Latest signup date to include.</param>
/// <param name="Page">One-based page number.</param>
/// <param name="PageSize">Rows per page.</param>
public record GetClientsReportQuery(
    string? Status = null,
    string? Country = null,
    DateOnly? CreatedFrom = null,
    DateOnly? CreatedTo = null,
    int Page = 1,
    int PageSize = 50);
