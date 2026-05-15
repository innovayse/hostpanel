namespace Innovayse.Application.Clients.Commands.TransferOwnership;

using Innovayse.Application.Common;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="TransferOwnershipCommand"/>.
/// Transfers client account ownership to a different user.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="uow">Unit of work.</param>
public sealed class TransferOwnershipHandler(
    IClientRepository clientRepo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Transfers account ownership to the specified user.
    /// </summary>
    /// <param name="cmd">The transfer ownership command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the client is not found or the user is already the owner.</exception>
    public async Task HandleAsync(TransferOwnershipCommand cmd, CancellationToken ct)
    {
        var client = await clientRepo.FindByIdAsync(cmd.ClientId, ct)
            ?? throw new InvalidOperationException($"Client {cmd.ClientId} not found.");

        client.TransferOwnership(cmd.NewOwnerUserId);
        await uow.SaveChangesAsync(ct);
    }
}
