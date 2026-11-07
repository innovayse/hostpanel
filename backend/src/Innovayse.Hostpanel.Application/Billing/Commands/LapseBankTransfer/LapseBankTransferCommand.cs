namespace Innovayse.Hostpanel.Application.Billing.Commands.LapseBankTransfer;

/// <summary>Command to end the wait of a manual bank transfer that stayed unconfirmed for too long (sent by the overdue job).</summary>
/// <param name="InvoiceId">The invoice whose bank transfer wait lapses.</param>
public sealed record LapseBankTransferCommand(int InvoiceId);
