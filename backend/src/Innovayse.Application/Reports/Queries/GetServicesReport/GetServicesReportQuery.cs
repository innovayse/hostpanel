namespace Innovayse.Application.Reports.Queries.GetServicesReport;

/// <summary>Query for the Services report with filters and paging.</summary>
/// <param name="Status">Service status to keep, or null for every status.</param>
/// <param name="BillingCycle">Billing cycle to keep, or null for every cycle.</param>
/// <param name="CreatedFrom">Earliest creation date to include.</param>
/// <param name="CreatedTo">Latest creation date to include.</param>
/// <param name="NextDueFrom">Earliest next-due date to include.</param>
/// <param name="NextDueTo">Latest next-due date to include.</param>
/// <param name="TerminatedFrom">Earliest termination date to include.</param>
/// <param name="TerminatedTo">Latest termination date to include.</param>
/// <param name="Page">One-based page number.</param>
/// <param name="PageSize">Rows per page.</param>
public record GetServicesReportQuery(
    string? Status = null,
    string? BillingCycle = null,
    DateOnly? CreatedFrom = null,
    DateOnly? CreatedTo = null,
    DateOnly? NextDueFrom = null,
    DateOnly? NextDueTo = null,
    DateOnly? TerminatedFrom = null,
    DateOnly? TerminatedTo = null,
    int Page = 1,
    int PageSize = 50);
