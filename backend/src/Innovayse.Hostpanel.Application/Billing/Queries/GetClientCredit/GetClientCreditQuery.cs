namespace Innovayse.Hostpanel.Application.Billing.Queries.GetClientCredit;

/// <summary>Query for one client's credit as staff see it: the balance and a page of the history.</summary>
/// <param name="ClientId">The client; the route's id wins over any value in the query string.</param>
/// <param name="Page">1-based page number of the history.</param>
/// <param name="PageSize">Number of history lines per page (clamped by the handler).</param>
public sealed record GetClientCreditQuery(int ClientId, int Page = 1, int PageSize = 25);
