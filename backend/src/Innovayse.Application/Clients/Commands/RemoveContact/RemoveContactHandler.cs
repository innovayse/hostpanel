namespace Innovayse.Application.Clients.Commands.RemoveContact;

using Innovayse.Application.Clients.Common;
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
    /// <exception cref="InvalidOperationException">Thrown when the client is not found.</exception>
    /// <exception cref="MyContactNotFoundException">
    /// Thrown when the client has no contact with that id -- whether it is another account's or
    /// does not exist at all. One answer for both, deliberately.
    /// </exception>
    public async Task HandleAsync(RemoveContactCommand cmd, CancellationToken ct)
    {
        var client = await clientRepo.FindByIdAsync(cmd.ClientId, ct)
            ?? throw new InvalidOperationException($"Client {cmd.ClientId} not found.");

        // Checked here rather than left to the aggregate. Client.RemoveContact raises a bare
        // InvalidOperationException naming the id, which reaches the caller as a 400 carrying
        // INVALID_OPERATION and a hardcoded English sentence with the probed id in it. A contact
        // that is somebody else's and one that does not exist must be a single answer.
        if (client.Contacts.All(contact => contact.Id != cmd.ContactId))
            throw new MyContactNotFoundException(cmd.ContactId);

        client.RemoveContact(cmd.ContactId);
        await uow.SaveChangesAsync(ct);
    }
}
