namespace Innovayse.Application.Billing.Queries.GetMyInvoices;

/// <summary>Query to retrieve every invoice belonging to the calling client.</summary>
/// <remarks>
/// Carries no client id. Which account is resolved inside the handler from the credential.
/// </remarks>
public record GetMyInvoicesQuery();
