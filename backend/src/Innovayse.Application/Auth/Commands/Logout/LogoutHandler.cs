namespace Innovayse.Application.Auth.Commands.Logout;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;

/// <summary>
/// Handles <see cref="LogoutCommand"/>.
/// Revokes the provided refresh token so it cannot be used again.
/// </summary>
/// <param name="refreshTokenRepo">Refresh token persistence.</param>
/// <param name="uow">Unit of work.</param>
public sealed class LogoutHandler(IRefreshTokenRepository refreshTokenRepo, IUnitOfWork uow)
{
    /// <summary>
    /// Revokes the refresh token and saves changes.
    /// </summary>
    /// <param name="cmd">The logout command with the token to revoke.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(LogoutCommand cmd, CancellationToken ct)
    {
        await refreshTokenRepo.RevokeAsync(cmd.RefreshToken, ct);
        await uow.SaveChangesAsync(ct);
    }
}
