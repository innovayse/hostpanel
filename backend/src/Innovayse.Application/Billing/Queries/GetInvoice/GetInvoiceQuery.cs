namespace Innovayse.Application.Billing.Queries.GetInvoice;

/// <summary>Query to retrieve a single invoice with its line items.</summary>
/// <param name="InvoiceId">The invoice primary key.</param>
public record GetInvoiceQuery(int InvoiceId);
