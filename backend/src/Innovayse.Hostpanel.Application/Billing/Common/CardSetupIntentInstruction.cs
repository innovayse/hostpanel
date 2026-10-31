namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>What the card gateway needs to open a setup intent, the flow that saves a card without a payment.</summary>
/// <param name="CustomerId">The card gateway customer the card will be saved against.</param>
/// <param name="ClientId">The client adding the card, kept on the intent for the gateway's dashboard.</param>
/// <param name="IdempotencyKey">The key the gateway deduplicates the setup intent by.</param>
public sealed record CardSetupIntentInstruction(string CustomerId, int ClientId, string IdempotencyKey);
