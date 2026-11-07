namespace Innovayse.Hostpanel.Application.Billing.Commands.ConfirmBankTransfer;

/// <summary>Command for staff to confirm that the manual bank transfer of an invoice arrived.</summary>
/// <param name="InvoiceId">The invoice whose transfer arrived.</param>
public sealed record ConfirmBankTransferCommand(int InvoiceId);
