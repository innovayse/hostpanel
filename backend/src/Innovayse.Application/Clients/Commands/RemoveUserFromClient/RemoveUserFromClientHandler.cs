namespace Innovayse.Application.Clients.Commands.RemoveUserFromClient;

using Innovayse.Application.Common;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="RemoveUserFromClientCommand"/>.
/// Removes a non-owner user from the client's user list.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="uow">Unit of work.</param>
public sealed class RemoveUserFromClientHandler(
    IClientRepository clientRepo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Removes a non-owner user from the client account.
    /// </summary>
    /// <param name="cmd">The remove user command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the client or user link is not found.</exception>
    public async Task HandleAsync(RemoveUserFromClientCommand cmd, CancellationToken ct)
    {
        var client = await clientRepo.FindByIdAsync(cmd.ClientId, ct)
            ?? throw new InvalidOperationException($"Client {cmd.ClientId} not found.");

        client.RemoveUser(cmd.UserId);
        await uow.SaveChangesAsync(ct);
    }
}
