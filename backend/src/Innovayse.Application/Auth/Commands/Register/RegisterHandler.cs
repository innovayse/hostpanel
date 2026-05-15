namespace Innovayse.Application.Auth.Commands.Register;

using Innovayse.Application.Auth.DTOs;
using Innovayse.Application.Auth.Events;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Domain.Auth;
using Wolverine;

/// <summary>
/// Handles <see cref="RegisterCommand"/>.
/// Creates a new Identity user with the Client role, issues tokens,
/// and publishes <see cref="ClientRegisteredIntegrationEvent"/> so the Clients module
/// can create the <c>Client</c> record.
/// </summary>
/// <param name="userService">Identity user management abstraction.</param>
/// <param name="tokenService">JWT token generation service.</param>
/// <param name="refreshTokenRepo">Refresh token persistence.</param>
/// <param name="uow">Unit of work for saving the refresh token.</param>
/// <param name="bus">Wolverine message bus for publishing integration events.</param>
public sealed class RegisterHandler(
    IUserService userService,
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokenRepo,
    IUnitOfWork uow,
    IMessageBus bus)
{
    /// <summary>
    /// Executes the registration: creates user, assigns Client role, issues tokens,
    /// and publishes <see cref="ClientRegisteredIntegrationEvent"/>.
    /// </summary>
    /// <param name="cmd">The registration command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Auth result containing the access token and refresh token string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when user creation fails.</exception>
    public async Task<AuthWithRefreshDto> HandleAsync(RegisterCommand cmd, CancellationToken ct)
    {
        var userId = await userService.CreateAsync(cmd.Email, cmd.Password, ct);
        await userService.AddToRoleAsync(userId, Roles.Client, ct);

        var (accessToken, expiresAt) = tokenService.GenerateAccessToken(userId, cmd.Email, Roles.Client);
        var (refreshToken, refreshExpiresAt) = tokenService.GenerateRefreshToken(userId);

        await refreshTokenRepo.AddAsync(userId, refreshToken, refreshExpiresAt, ct);
        await uow.SaveChangesAsync(ct);

        await bus.PublishAsync(new ClientRegisteredIntegrationEvent(userId, cmd.Email, cmd.FirstName, cmd.LastName));

        return new AuthWithRefreshDto(new AuthResultDto(accessToken, expiresAt, Roles.Client), refreshToken);
    }
}
