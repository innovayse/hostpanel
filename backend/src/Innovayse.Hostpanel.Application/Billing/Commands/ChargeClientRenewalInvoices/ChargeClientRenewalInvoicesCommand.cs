namespace Innovayse.Hostpanel.Application.Billing.Commands.ChargeClientRenewalInvoices;

/// <summary>
/// Charges the open renewal invoices of one client to the stored payment method the client consented to. Its own
/// message, like raising the invoices, so each client runs in its own Wolverine scope and one client's failure cannot
/// leak tracked entities into the next client's save. A client with no consent costs one cheap read and nothing else.
/// </summary>
/// <param name="ClientId">The client.</param>
public sealed record ChargeClientRenewalInvoicesCommand(int ClientId);
