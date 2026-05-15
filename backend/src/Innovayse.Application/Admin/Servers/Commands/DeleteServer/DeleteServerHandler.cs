namespace Innovayse.Application.Admin.Servers.Commands.DeleteServer;
using Innovayse.Application.Common;
using Innovayse.Domain.Servers.Interfaces;

/// <summary>
/// Handles <see cref="DeleteServerCommand"/> by removing the server from the repository.
/// </summary>
public sealed class DeleteServerHandler(IServerRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Deletes the server, throwing if not found.
    /// </summary>
    /// <param name="cmd">The delete command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when no server with the given ID exists.</exception>
    public async Task HandleAsync(DeleteServerCommand cmd, CancellationToken ct)
    {
        var server = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Server {cmd.Id} not found.");

        repo.Remove(server);
        await uow.SaveChangesAsync(ct);
    }
}
