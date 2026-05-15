namespace Innovayse.Application.Auth.DTOs;

/// <summary>
/// Result returned to the client after a successful login or token refresh.
/// The refresh token is NOT included here — it is set as an httpOnly cookie by the API layer.
/// </summary>
/// <param name="AccessToken">Signed JWT access token. Valid for 15 minutes.</param>
/// <param name="ExpiresAt">UTC timestamp when the access token expires.</param>
/// <param name="Role">The primary role of the authenticated user.</param>
public record AuthResultDto(string AccessToken, DateTimeOffset ExpiresAt, string Role);
