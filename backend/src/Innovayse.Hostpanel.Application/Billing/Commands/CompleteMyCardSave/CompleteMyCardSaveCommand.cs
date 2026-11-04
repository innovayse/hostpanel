namespace Innovayse.Hostpanel.Application.Billing.Commands.CompleteMyCardSave;

/// <summary>
/// Finishes the "Add card" flow: asks the gateway module to complete the save session and, only when the gateway
/// says the method was saved, makes the caller's agreement active and the method the default when it is the first.
/// </summary>
/// <remarks>Carries no user id: whose account is resolved inside the handler from the credential.</remarks>
/// <param name="GatewayModuleId">The payment gateway module the method was saved with.</param>
/// <param name="SessionId">The id <c>BeginMyCardSave</c> answered with.</param>
public sealed record CompleteMyCardSaveCommand(string GatewayModuleId, string SessionId);
