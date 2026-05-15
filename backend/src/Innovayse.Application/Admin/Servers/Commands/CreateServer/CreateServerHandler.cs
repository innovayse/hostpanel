namespace Innovayse.Application.Admin.Servers.Commands.CreateServer;
using Innovayse.Application.Common;
using Innovayse.Domain.Servers;
using Innovayse.Domain.Servers.Interfaces;

/// <summary>
/// Handles <see cref="CreateServerCommand"/> by creating and persisting a new server.
/// </summary>
public sealed class CreateServerHandler(IServerRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Creates the server and returns its assigned identifier.
    /// </summary>
    /// <param name="cmd">The create command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new server's identifier.</returns>
    public async Task<int> HandleAsync(CreateServerCommand cmd, CancellationToken ct)
    {
        var server = Server.Create(cmd.Details);
        repo.Add(server);
        await uow.SaveChangesAsync(ct);
        return server.Id;
    }
}
