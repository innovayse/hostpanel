namespace Innovayse.Hostpanel.Application.Billing.Commands.CompleteSaveStoredMethod;

/// <summary>
/// Finishes a save session after the payer entered the method (in either mode) and binds the stored method to the
/// consent recorded when the session was opened. Safe to send again for the same session.
/// </summary>
/// <remarks>Carries no client id: the consent is found for the caller's own account only.</remarks>
/// <param name="GatewayModuleId">The payment plugin id the session was opened at.</param>
/// <param name="SessionId">The session id from <c>BeginSaveStoredMethod</c>.</param>
public sealed record CompleteSaveStoredMethodCommand(string GatewayModuleId, string SessionId);
