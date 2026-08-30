namespace Innovayse.Application.Clients.Commands.UpdateContact;

using Innovayse.Application.Clients.Common;
using Innovayse.Application.Common;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="UpdateContactCommand"/>.
/// Loads the client aggregate and updates the specified contact.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="uow">Unit of work.</param>
public sealed class UpdateContactHandler(IClientRepository clientRepo, IUnitOfWork uow)
{
    /// <summary>
    /// Updates a contact on the specified client and saves.
    /// </summary>
    /// <param name="cmd">The update contact command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the client is not found.</exception>
    /// <exception cref="MyContactNotFoundException">
    /// Thrown when the client has no contact with that id -- whether it is another account's or
    /// does not exist at all. One answer for both, deliberately.
    /// </exception>
    public async Task HandleAsync(UpdateContactCommand cmd, CancellationToken ct)
    {
        var client = await clientRepo.FindByIdAsync(cmd.ClientId, ct)
            ?? throw new InvalidOperationException($"Client {cmd.ClientId} not found.");

        // Checked here rather than left to the aggregate. Client.UpdateContact raises a bare
        // InvalidOperationException naming the id, which reaches the caller as a 400 carrying
        // INVALID_OPERATION and a hardcoded English sentence with the probed id in it. A contact
        // that is somebody else's and one that does not exist must be a single answer.
        if (client.Contacts.All(contact => contact.Id != cmd.ContactId))
            throw new MyContactNotFoundException(cmd.ContactId);

        client.UpdateContact(
            cmd.ContactId,
            cmd.FirstName, cmd.LastName, cmd.CompanyName,
            cmd.Email, cmd.Phone, cmd.Type,
            cmd.Street, cmd.Address2, cmd.City, cmd.State, cmd.PostCode, cmd.Country,
            cmd.NotifyGeneral, cmd.NotifyInvoice, cmd.NotifySupport,
            cmd.NotifyProduct, cmd.NotifyDomain, cmd.NotifyAffiliate);

        await uow.SaveChangesAsync(ct);
    }
}
