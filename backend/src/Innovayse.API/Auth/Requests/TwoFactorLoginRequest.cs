namespace Innovayse.API.Auth.Requests;

/// <summary>
/// Request body for the <c>POST /api/auth/2fa/login</c> endpoint.
/// </summary>
/// <param name="PendingToken">The pre-auth token returned by the login endpoint when 2FA is required.</param>
/// <param name="Code">The 6-digit TOTP code from the user's authenticator app.</param>
public record TwoFactorLoginRequest(string PendingToken, string Code);
