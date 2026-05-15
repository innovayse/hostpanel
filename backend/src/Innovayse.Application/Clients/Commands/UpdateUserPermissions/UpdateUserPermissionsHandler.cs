namespace Innovayse.Application.Clients.Commands.UpdateUserPermissions;

using Innovayse.Application.Common;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="UpdateUserPermissionsCommand"/>.
/// Updates the permissions for a non-owner user linked to a client.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="uow">Unit of work.</param>
public sealed class UpdateUserPermissionsHandler(
    IClientRepository clientRepo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Updates a non-owner user's permissions on the client account.
    /// </summary>
    /// <param name="cmd">The update permissions command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the client or user link is not found.</exception>
    public async Task HandleAsync(UpdateUserPermissionsCommand cmd, CancellationToken ct)
    {
        var client = await clientRepo.FindByIdAsync(cmd.ClientId, ct)
            ?? throw new InvalidOperationException($"Client {cmd.ClientId} not found.");

        if ((cmd.Permissions & ~(int)ClientPermission.All) != 0)
            throw new InvalidOperationException($"Invalid permissions value: {cmd.Permissions}.");

        client.UpdateUserPermissions(cmd.UserId, (ClientPermission)cmd.Permissions);
        await uow.SaveChangesAsync(ct);
    }
}
