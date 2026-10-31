namespace Innovayse.Hostpanel.Application.Billing.Commands.ConfirmMySetupIntent;

/// <summary>
/// Finishes the "Add card" flow: reads the setup intent back from the card gateway and, only when the
/// gateway says the card was saved, makes the caller's agreement active and the card the default when it
/// is the first.
/// </summary>
/// <remarks>Carries no user id: whose account is resolved inside the handler from the credential.</remarks>
/// <param name="SetupIntentId">The id <c>CreateMySetupIntent</c> answered with.</param>
public sealed record ConfirmMySetupIntentCommand(string SetupIntentId);
