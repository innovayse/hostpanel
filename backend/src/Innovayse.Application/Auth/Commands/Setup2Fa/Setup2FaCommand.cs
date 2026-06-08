namespace Innovayse.Application.Auth.Commands.Setup2Fa;

/// <summary>
/// Generates a new TOTP secret for the user and returns the QR code URI for authenticator app setup.
/// Does NOT enable 2FA — the user must confirm with a valid TOTP code via <c>Enable2FaCommand</c> first.
/// </summary>
/// <param name="UserId">The authenticated user's ID.</param>
/// <param name="Email">The user's email address, used in the <c>otpauth://</c> URI label.</param>
public record Setup2FaCommand(string UserId, string Email);
