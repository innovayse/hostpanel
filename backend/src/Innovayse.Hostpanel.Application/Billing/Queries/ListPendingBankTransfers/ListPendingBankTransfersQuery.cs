namespace Innovayse.Hostpanel.Application.Billing.Queries.ListPendingBankTransfers;

/// <summary>Paginated query for the manual bank transfers that wait for staff: the admin "Pending bank transfers" view.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Number of items per page (clamped by the handler).</param>
public sealed record ListPendingBankTransfersQuery(int Page = 1, int PageSize = 25);
