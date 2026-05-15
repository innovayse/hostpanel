namespace Innovayse.Application.Auth.Commands.RefreshToken;

using Innovayse.Application.Auth.DTOs;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;

/// <summary>
/// Handles <see cref="RefreshTokenCommand"/>.
/// Validates the old refresh token, rotates it, and issues a new access token.
/// </summary>
/// <param name="tokenService">Token generation and validation.</param>
/// <param name="refreshTokenRepo">Refresh token persistence.</param>
/// <param name="userService">Abstraction over Identity user management.</param>
/// <param name="uow">Unit of work.</param>
public sealed class RefreshTokenHandler(
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokenRepo,
    IUserService userService,
    IUnitOfWork uow)
{
    /// <summary>
    /// Validates the refresh token, revokes it (rotation), and issues a new pair.
    /// </summary>
    /// <param name="cmd">The refresh command containing the old token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>New auth result and new raw refresh token string.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the token is invalid or expired.</exception>
    public async Task<AuthWithRefreshDto> HandleAsync(RefreshTokenCommand cmd, CancellationToken ct)
    {
        var userId = await tokenService.ValidateRefreshTokenAsync(cmd.RefreshToken, ct)
            ?? throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        await refreshTokenRepo.RevokeAsync(cmd.RefreshToken, ct);

        var found = await userService.FindByIdAsync(userId, ct)
            ?? throw new UnauthorizedAccessException("User not found.");

        var primaryRole = await userService.GetPrimaryRoleAsync(found.Id, ct) ?? "Client";

        var (accessToken, expiresAt) = tokenService.GenerateAccessToken(found.Id, found.Email, primaryRole);
        var (newRefreshToken, refreshExpiresAt) = tokenService.GenerateRefreshToken(found.Id);

        await refreshTokenRepo.AddAsync(found.Id, newRefreshToken, refreshExpiresAt, ct);
        await uow.SaveChangesAsync(ct);

        return new AuthWithRefreshDto(new AuthResultDto(accessToken, expiresAt, primaryRole), newRefreshToken);
    }
}
