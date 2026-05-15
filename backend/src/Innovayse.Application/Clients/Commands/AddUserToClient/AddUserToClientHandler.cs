namespace Innovayse.Application.Clients.Commands.AddUserToClient;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="AddUserToClientCommand"/>.
/// Verifies the user exists, then adds them to the client with the specified permissions.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="userService">Identity user service for existence verification.</param>
/// <param name="uow">Unit of work.</param>
public sealed class AddUserToClientHandler(
    IClientRepository clientRepo,
    IUserService userService,
    IUnitOfWork uow)
{
    /// <summary>
    /// Adds a non-owner user to the client account.
    /// </summary>
    /// <param name="cmd">The add user command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the client or user is not found.</exception>
    public async Task HandleAsync(AddUserToClientCommand cmd, CancellationToken ct)
    {
        var client = await clientRepo.FindByIdAsync(cmd.ClientId, ct)
            ?? throw new InvalidOperationException($"Client {cmd.ClientId} not found.");

        var user = await userService.FindByIdAsync(cmd.UserId, ct)
            ?? throw new InvalidOperationException($"User {cmd.UserId} not found.");

        if ((cmd.Permissions & ~(int)ClientPermission.All) != 0)
            throw new InvalidOperationException($"Invalid permissions value: {cmd.Permissions}.");

        client.AddUser(cmd.UserId, (ClientPermission)cmd.Permissions);
        await uow.SaveChangesAsync(ct);
    }
}
