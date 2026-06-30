namespace Innovayse.Application.Auth.Commands.Login;

using Innovayse.Application.Auth.DTOs;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;

/// <summary>
/// Handles <see cref="LoginCommand"/>.
/// Verifies credentials via Identity, then either issues a JWT + refresh token pair
/// or returns a pending 2FA token if two-factor authentication is enabled on the account.
/// </summary>
/// <param name="userService">Abstraction over Identity user management.</param>
/// <param name="tokenService">JWT token generation service.</param>
/// <param name="refreshTokenRepo">Refresh token persistence.</param>
/// <param name="uow">Unit of work for saving the refresh token.</param>
/// <param name="twoFactorPendingCache">Cache for pre-auth tokens issued when 2FA is required.</param>
public sealed class LoginHandler(
    IUserService userService,
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokenRepo,
    IUnitOfWork uow,
    ITwoFactorPendingCache twoFactorPendingCache)
{
    /// <summary>
    /// Authenticates the user and returns either a full token pair or a pending 2FA challenge.
    /// </summary>
    /// <param name="cmd">The login command with email and password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="LoginResultDto"/> where <c>TwoFactorRequired</c> is true and <c>PendingToken</c>
    /// is set when the account has 2FA enabled, or <c>Auth</c> and <c>RefreshToken</c> are set
    /// when login completes in a single step.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when credentials are invalid.</exception>
    public async Task<LoginResultDto> HandleAsync(LoginCommand cmd, CancellationToken ct)
    {
        var found = await userService.FindByEmailAndPasswordAsync(cmd.Email, cmd.Password, ct)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        var twoFactorEnabled = await userService.IsTwoFactorEnabledAsync(found.Id, ct);

        if (twoFactorEnabled)
        {
            var pendingToken = twoFactorPendingCache.CreatePendingToken(found.Id);
            return new LoginResultDto(null, null, true, pendingToken);
        }

        var primaryRole = await userService.GetPrimaryRoleAsync(found.Id, ct) ?? "Client";
        var (accessToken, expiresAt) = tokenService.GenerateAccessToken(found.Id, found.Email, primaryRole);
        var (refreshToken, refreshExpiresAt) = tokenService.GenerateRefreshToken(found.Id);

        await refreshTokenRepo.AddAsync(found.Id, refreshToken, refreshExpiresAt, ct);
        await uow.SaveChangesAsync(ct);
        await userService.UpdateLastLoginAsync(found.Id, ct);

        return new LoginResultDto(new AuthResultDto(accessToken, expiresAt, primaryRole), refreshToken, false, null);
    }
}
