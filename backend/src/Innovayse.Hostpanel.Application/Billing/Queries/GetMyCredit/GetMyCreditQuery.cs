namespace Innovayse.Hostpanel.Application.Billing.Queries.GetMyCredit;

/// <summary>Query for the calling client's own credit: the balance, the top-up limits and a page of the history.</summary>
/// <remarks>
/// Carries no client id. Which account is read is resolved inside the handler from the credential.
/// </remarks>
/// <param name="Page">1-based page number of the history.</param>
/// <param name="PageSize">Number of history lines per page (clamped by the handler).</param>
public sealed record GetMyCreditQuery(int Page = 1, int PageSize = 25);
