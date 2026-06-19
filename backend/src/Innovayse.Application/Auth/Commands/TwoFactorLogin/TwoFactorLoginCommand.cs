namespace Innovayse.Application.Auth.Commands.TwoFactorLogin;

/// <summary>
/// Completes a login attempt by verifying a TOTP code against a pre-auth pending token.
/// Called after a login attempt returns <c>twoFactorRequired = true</c>.
/// </summary>
/// <param name="PendingToken">The short-lived pre-auth token returned by the login endpoint.</param>
/// <param name="Code">The 6-digit TOTP code from the user's authenticator app.</param>
public record TwoFactorLoginCommand(string PendingToken, string Code);
