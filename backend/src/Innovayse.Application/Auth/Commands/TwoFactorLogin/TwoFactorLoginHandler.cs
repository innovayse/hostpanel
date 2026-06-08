namespace Innovayse.Application.Auth.Commands.TwoFactorLogin;

using Innovayse.Application.Auth.DTOs;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;

/// <summary>
/// Handles <see cref="TwoFactorLoginCommand"/>.
/// Consumes the pending pre-auth token, verifies the TOTP code, and issues full auth tokens.
/// </summary>
/// <param name="pendingCache">Cache that holds pre-auth sessions for users mid-2FA-flow.</param>
/// <param name="userService">Abstraction over Identity user management.</param>
/// <param name="tokenService">JWT token generation service.</param>
/// <param name="refreshTokenRepo">Refresh token persistence.</param>
/// <param name="uow">Unit of work for saving the refresh token.</param>
public sealed class TwoFactorLoginHandler(
    ITwoFactorPendingCache pendingCache,
    IUserService userService,
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokenRepo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Verifies the TOTP code and issues JWT + refresh token on success.
    /// </summary>
    /// <param name="cmd">The command carrying the pending token and TOTP code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Full auth result with access token and raw refresh token.</returns>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the pending token is invalid/expired or the TOTP code does not match.
    /// </exception>
    public async Task<AuthWithRefreshDto> HandleAsync(TwoFactorLoginCommand cmd, CancellationToken ct)
    {
        var userId = pendingCache.ConsumePendingToken(cmd.PendingToken)
            ?? throw new UnauthorizedAccessException("Invalid or expired 2FA session.");

        var valid = await userService.VerifyTwoFactorCodeAsync(userId, cmd.Code, ct);
        if (!valid)
        {
            throw new UnauthorizedAccessException("Invalid 2FA code.");
        }

        var found = await userService.FindByIdAsync(userId, ct)
            ?? throw new UnauthorizedAccessException("User not found.");

        var primaryRole = await userService.GetPrimaryRoleAsync(userId, ct) ?? "Client";
        var (accessToken, expiresAt) = tokenService.GenerateAccessToken(userId, found.Email, primaryRole);
        var (refreshToken, refreshExpiresAt) = tokenService.GenerateRefreshToken(userId);

        await refreshTokenRepo.AddAsync(userId, refreshToken, refreshExpiresAt, ct);
        await uow.SaveChangesAsync(ct);
        await userService.UpdateLastLoginAsync(userId, ct);

        return new AuthWithRefreshDto(new AuthResultDto(accessToken, expiresAt, primaryRole), refreshToken);
    }
}
