namespace Innovayse.Application.Auth.Commands.Logout;

/// <summary>
/// Command to revoke the current user's refresh token, effectively logging them out.
/// </summary>
/// <param name="RefreshToken">The raw refresh token string from the httpOnly cookie.</param>
public record LogoutCommand(string RefreshToken);
