namespace Innovayse.Application.Clients.Commands.UpdateContact;

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
    /// <exception cref="InvalidOperationException">Thrown when the client or contact is not found.</exception>
    public async Task HandleAsync(UpdateContactCommand cmd, CancellationToken ct)
    {
        var client = await clientRepo.FindByIdAsync(cmd.ClientId, ct)
            ?? throw new InvalidOperationException($"Client {cmd.ClientId} not found.");

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
