namespace Innovayse.Hostpanel.Application.Billing.Commands.CreateMyCreditTopUp;

/// <summary>Command for a client to add funds to their own credit balance by raising a top-up invoice they then pay.</summary>
/// <remarks>
/// Carries no client id. Which account the invoice is raised for is resolved inside the handler from the credential.
/// </remarks>
/// <param name="Amount">The amount to add, in the client's billing currency; inside the limits the admin set.</param>
public sealed record CreateMyCreditTopUpCommand(decimal Amount);
