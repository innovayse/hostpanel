namespace Innovayse.Application.Billing.Queries.GetMyInvoice;

/// <summary>Query to retrieve one of the calling client's own invoices.</summary>
/// <remarks>
/// Carries an invoice id but no client id. Which account the invoice must belong to is resolved
/// inside the handler from the credential, so the scoping cannot be separated from the message
/// the way an id in the body can. The admin read that may return any invoice is a separate use
/// case, <c>GetInvoiceQuery</c>.
/// </remarks>
/// <param name="InvoiceId">The invoice primary key.</param>
public sealed record GetMyInvoiceQuery(int InvoiceId);
