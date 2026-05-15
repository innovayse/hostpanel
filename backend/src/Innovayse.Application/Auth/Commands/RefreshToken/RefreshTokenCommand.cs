namespace Innovayse.Application.Auth.Commands.RefreshToken;

/// <summary>
/// Command to exchange a valid refresh token for a new access token and rotated refresh token.
/// </summary>
/// <param name="RefreshToken">The raw refresh token string from the httpOnly cookie.</param>
public record RefreshTokenCommand(string RefreshToken);
