namespace Innovayse.Hostpanel.Application.Billing.Queries.ListInvoiceFulfilment;

/// <summary>Query for the fulfilment records of one invoice, for staff.</summary>
/// <param name="InvoiceId">The invoice whose records to list.</param>
public sealed record ListInvoiceFulfilmentQuery(int InvoiceId);
