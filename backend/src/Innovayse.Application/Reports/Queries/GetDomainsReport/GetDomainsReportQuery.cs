namespace Innovayse.Application.Reports.Queries.GetDomainsReport;

/// <summary>Query for the Domains report with filters and paging.</summary>
/// <param name="Status">Domain status to keep, or null for every status.</param>
/// <param name="Registrar">Registrar to keep, or null for every registrar.</param>
/// <param name="RegisteredFrom">Earliest registration date to include.</param>
/// <param name="RegisteredTo">Latest registration date to include.</param>
/// <param name="ExpiresFrom">Earliest expiry date to include.</param>
/// <param name="ExpiresTo">Latest expiry date to include.</param>
/// <param name="NextDueFrom">Earliest next-due date to include.</param>
/// <param name="NextDueTo">Latest next-due date to include.</param>
/// <param name="Page">One-based page number.</param>
/// <param name="PageSize">Rows per page.</param>
public record GetDomainsReportQuery(
    string? Status = null,
    string? Registrar = null,
    DateOnly? RegisteredFrom = null,
    DateOnly? RegisteredTo = null,
    DateOnly? ExpiresFrom = null,
    DateOnly? ExpiresTo = null,
    DateOnly? NextDueFrom = null,
    DateOnly? NextDueTo = null,
    int Page = 1,
    int PageSize = 50);
