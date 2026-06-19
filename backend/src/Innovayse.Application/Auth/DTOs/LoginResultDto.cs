namespace Innovayse.Application.Auth.DTOs;

/// <summary>
/// Result of a login attempt. Either carries full auth tokens or signals that a TOTP code is required.
/// </summary>
/// <param name="Auth">Populated when 2FA is not required or not enabled. Null when <paramref name="TwoFactorRequired"/> is true.</param>
/// <param name="RefreshToken">Raw refresh token string to be set as an httpOnly cookie by the API layer. Null when <paramref name="TwoFactorRequired"/> is true.</param>
/// <param name="TwoFactorRequired">True when the account has 2FA enabled and a TOTP code must be verified before tokens are issued.</param>
/// <param name="PendingToken">Short-lived token representing the pre-authenticated session. Only populated when <paramref name="TwoFactorRequired"/> is true.</param>
public record LoginResultDto(
    AuthResultDto? Auth,
    string? RefreshToken,
    bool TwoFactorRequired,
    string? PendingToken);
