namespace Innovayse.Application.Clients.Events;

using Innovayse.Application.Auth.Events;
using Innovayse.Application.Common;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Wolverine handler that reacts to <see cref="ClientRegisteredIntegrationEvent"/>.
/// Automatically creates a <see cref="Client"/> record for every new registered user.
/// This handler is idempotent: if a client record already exists for the user, it does nothing.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="uow">Unit of work.</param>
public sealed class CreateClientOnRegisterHandler(IClientRepository clientRepo, IUnitOfWork uow)
{
    /// <summary>
    /// Creates a Client record when a new user registers.
    /// </summary>
    /// <param name="evt">The integration event published by RegisterHandler.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(ClientRegisteredIntegrationEvent evt, CancellationToken ct)
    {
        // Idempotency: skip if the client record already exists
        var existing = await clientRepo.FindByUserIdAsync(evt.UserId, ct);
        if (existing is not null)
        {
            return;
        }

        var client = Client.Create(evt.UserId, evt.FirstName, evt.LastName, evt.Email);
        clientRepo.Add(client);
        await uow.SaveChangesAsync(ct);
    }
}
