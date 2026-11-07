namespace Innovayse.Hostpanel.Application.Billing.Commands.SelectMyBankTransfer;

/// <summary>Command for a client to choose manual bank transfer as the way to pay one of their own invoices.</summary>
/// <remarks>
/// Carries no client id. Which account the invoice must belong to is resolved inside the handler from the credential.
/// </remarks>
/// <param name="InvoiceId">The invoice to pay by bank transfer.</param>
public sealed record SelectMyBankTransferCommand(int InvoiceId);
