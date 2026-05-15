namespace Innovayse.Application.Clients.Commands.CreateClient;

using Innovayse.Application.Common;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="CreateClientCommand"/>.
/// Creates and persists a new <see cref="Client"/> aggregate.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="uow">Unit of work.</param>
public sealed class CreateClientHandler(IClientRepository clientRepo, IUnitOfWork uow)
{
    /// <summary>
    /// Creates a new client record and saves it.
    /// </summary>
    /// <param name="cmd">The create client command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ID of the newly created client.</returns>
    public async Task<int> HandleAsync(CreateClientCommand cmd, CancellationToken ct)
    {
        var client = Client.Create(cmd.UserId, cmd.FirstName, cmd.LastName, cmd.Email, cmd.CompanyName);
        clientRepo.Add(client);
        await uow.SaveChangesAsync(ct);
        return client.Id;
    }
}
