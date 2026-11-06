namespace Innovayse.Hostpanel.Application.Billing.Queries.GetMyBankTransfer;

/// <summary>Query for the bank details and reference of the manual bank transfer waiting on one of the calling client's invoices.</summary>
/// <remarks>Carries no client id: which account the invoice must belong to is resolved inside the handler from the credential.</remarks>
/// <param name="InvoiceId">The invoice primary key.</param>
public sealed record GetMyBankTransferQuery(int InvoiceId);
