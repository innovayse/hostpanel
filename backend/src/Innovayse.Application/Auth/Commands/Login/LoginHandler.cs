namespace Innovayse.Application.Auth.Commands.Login;

using Innovayse.Application.Auth.DTOs;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;

/// <summary>
/// Handles <see cref="LoginCommand"/>.
/// Verifies credentials via Identity, then issues a JWT + refresh token pair.
/// </summary>
/// <param name="userService">Abstraction over Identity user management.</param>
/// <param name="tokenService">JWT token generation service.</param>
/// <param name="refreshTokenRepo">Refresh token persistence.</param>
/// <param name="uow">Unit of work for saving the refresh token.</param>
public sealed class LoginHandler(
    IUserService userService,
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokenRepo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Authenticates the user and returns a token pair.
    /// </summary>
    /// <param name="cmd">The login command with email and password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Auth result and raw refresh token string.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when credentials are invalid.</exception>
    public async Task<AuthWithRefreshDto> HandleAsync(LoginCommand cmd, CancellationToken ct)
    {
        var found = await userService.FindByEmailAndPasswordAsync(cmd.Email, cmd.Password, ct)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        var primaryRole = await userService.GetPrimaryRoleAsync(found.Id, ct) ?? "Client";

        var (accessToken, expiresAt) = tokenService.GenerateAccessToken(found.Id, found.Email, primaryRole);
        var (refreshToken, refreshExpiresAt) = tokenService.GenerateRefreshToken(found.Id);

        await refreshTokenRepo.AddAsync(found.Id, refreshToken, refreshExpiresAt, ct);
        await uow.SaveChangesAsync(ct);

        await userService.UpdateLastLoginAsync(found.Id, ct);

        return new AuthWithRefreshDto(new AuthResultDto(accessToken, expiresAt, primaryRole), refreshToken);
    }
}
