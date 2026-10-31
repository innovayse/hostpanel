namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>What the card gateway needs to open a customer for a client.</summary>
/// <param name="ClientId">The client the customer is for; the gateway keeps it on the customer so a customer can be traced back to its client.</param>
/// <param name="Name">The client's name, shown in the gateway's dashboard; may be empty.</param>
/// <param name="Email">The client's e-mail address, shown in the gateway's dashboard; may be empty.</param>
/// <param name="IdempotencyKey">The key the gateway deduplicates the creation by, so two parallel requests for one client open one customer.</param>
public sealed record CardCreateCustomerInstruction(int ClientId, string Name, string Email, string IdempotencyKey);
