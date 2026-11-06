using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Application.Clients.Common;

/// <summary>A client's credit balance after a change was staged.</summary>
/// <param name="ClientId">The client the balance belongs to.</param>
/// <param name="Balance">The balance once the staged change is saved.</param>
/// <param name="Entry">The ledger entry that records the change: the one the caller gave and was stamped, or the credit-only entry the port wrote.</param>
public sealed record ClientCreditBalance(int ClientId, decimal Balance, Transaction? Entry = null);
