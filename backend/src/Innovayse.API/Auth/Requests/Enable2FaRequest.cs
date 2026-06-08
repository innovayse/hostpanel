namespace Innovayse.API.Auth.Requests;

/// <summary>
/// Request body for the <c>POST /api/auth/2fa/enable</c> endpoint.
/// </summary>
/// <param name="Code">The 6-digit TOTP code from the user's authenticator app, used to confirm setup before enabling 2FA.</param>
public record Enable2FaRequest(string Code);
