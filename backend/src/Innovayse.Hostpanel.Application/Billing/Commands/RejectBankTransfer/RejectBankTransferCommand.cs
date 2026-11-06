namespace Innovayse.Hostpanel.Application.Billing.Commands.RejectBankTransfer;

/// <summary>Command for staff to reject the manual bank transfer of an invoice because the money did not arrive.</summary>
/// <param name="InvoiceId">The invoice whose transfer is rejected.</param>
/// <param name="Note">The reason, which the client reads in the mail that tells them to pay again.</param>
public sealed record RejectBankTransferCommand(int InvoiceId, string Note);
