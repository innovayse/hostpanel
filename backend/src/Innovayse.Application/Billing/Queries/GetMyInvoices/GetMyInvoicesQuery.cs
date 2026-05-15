namespace Innovayse.Application.Billing.Queries.GetMyInvoices;

/// <summary>Query to retrieve all invoices for a specific client (client-portal view).</summary>
/// <param name="ClientId">The client's primary key.</param>
public record GetMyInvoicesQuery(int ClientId);
