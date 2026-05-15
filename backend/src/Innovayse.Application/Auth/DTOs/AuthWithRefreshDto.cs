namespace Innovayse.Application.Auth.DTOs;

/// <summary>
/// Combined handler return type carrying both the auth result and the raw refresh token string.
/// The API layer reads <see cref="RefreshToken"/> and sets it as an httpOnly cookie.
/// </summary>
/// <param name="Auth">The access token result sent to the client.</param>
/// <param name="RefreshToken">The raw refresh token string to be stored in an httpOnly cookie.</param>
public record AuthWithRefreshDto(AuthResultDto Auth, string RefreshToken);
