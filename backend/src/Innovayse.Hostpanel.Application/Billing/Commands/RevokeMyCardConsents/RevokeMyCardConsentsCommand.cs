namespace Innovayse.Hostpanel.Application.Billing.Commands.RevokeMyCardConsents;

/// <summary>
/// Withdraws the caller's agreement to automatic card charges: no saved card of the account is charged
/// without the client from now on. The cards stay saved and can still be paid with by hand.
/// </summary>
/// <remarks>Carries no user id: whose account is resolved inside the handler from the credential.</remarks>
public sealed record RevokeMyCardConsentsCommand();
