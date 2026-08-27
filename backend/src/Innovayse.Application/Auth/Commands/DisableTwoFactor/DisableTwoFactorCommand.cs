namespace Innovayse.Application.Auth.Commands.DisableTwoFactor;

/// <summary>Switches two-factor authentication off.</summary>
/// <param name="UserId">Whose account. Comes from the credential, never from the request body.</param>
/// <param name="Code">A current code from the authenticator app.</param>
public record DisableTwoFactorCommand(string UserId, string Code);
