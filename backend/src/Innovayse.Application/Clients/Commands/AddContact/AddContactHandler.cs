namespace Innovayse.Application.Clients.Commands.AddContact;

using Innovayse.Application.Common;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="AddContactCommand"/>.
/// Loads the client aggregate and appends a new contact.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="uow">Unit of work.</param>
public sealed class AddContactHandler(IClientRepository clientRepo, IUnitOfWork uow)
{
    /// <summary>
    /// Adds a contact to the specified client and saves.
    /// </summary>
    /// <param name="cmd">The add contact command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the client is not found.</exception>
    public async Task HandleAsync(AddContactCommand cmd, CancellationToken ct)
    {
        var client = await clientRepo.FindByIdAsync(cmd.ClientId, ct)
            ?? throw new InvalidOperationException($"Client {cmd.ClientId} not found.");

        client.AddContact(
            cmd.FirstName, cmd.LastName, cmd.CompanyName,
            cmd.Email, cmd.Phone, cmd.Type,
            cmd.Street, cmd.Address2, cmd.City, cmd.State, cmd.PostCode, cmd.Country,
            cmd.NotifyGeneral, cmd.NotifyInvoice, cmd.NotifySupport,
            cmd.NotifyProduct, cmd.NotifyDomain, cmd.NotifyAffiliate);

        await uow.SaveChangesAsync(ct);
    }
}
