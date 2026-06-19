namespace Innovayse.Application.Auth.Commands.Enable2Fa;

/// <summary>
/// Enables 2FA for the user after they confirm with a valid TOTP code.
/// The user must have completed setup via <c>Setup2FaCommand</c> first.
/// </summary>
/// <param name="UserId">The authenticated user's ID.</param>
/// <param name="Code">The 6-digit TOTP code to verify before enabling 2FA.</param>
public record Enable2FaCommand(string UserId, string Code);
