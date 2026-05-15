namespace Innovayse.Application.Clients.Commands.RemoveContact;

using Innovayse.Application.Common;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="RemoveContactCommand"/>.
/// Loads the client aggregate and removes the specified contact.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="uow">Unit of work.</param>
public sealed class RemoveContactHandler(IClientRepository clientRepo, IUnitOfWork uow)
{
    /// <summary>
    /// Removes a contact from the specified client and saves.
    /// </summary>
    /// <param name="cmd">The remove contact command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the client or contact is not found.</exception>
    public async Task HandleAsync(RemoveContactCommand cmd, CancellationToken ct)
    {
        var client = await clientRepo.FindByIdAsync(cmd.ClientId, ct)
            ?? throw new InvalidOperationException($"Client {cmd.ClientId} not found.");

        client.RemoveContact(cmd.ContactId);
        await uow.SaveChangesAsync(ct);
    }
}
