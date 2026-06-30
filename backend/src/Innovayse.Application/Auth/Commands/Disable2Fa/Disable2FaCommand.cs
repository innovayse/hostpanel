namespace Innovayse.Application.Auth.Commands.Disable2Fa;

/// <summary>
/// Disables two-factor authentication and clears the stored TOTP secret for the user.
/// </summary>
/// <param name="UserId">The authenticated user's ID.</param>
public record Disable2FaCommand(string UserId);
