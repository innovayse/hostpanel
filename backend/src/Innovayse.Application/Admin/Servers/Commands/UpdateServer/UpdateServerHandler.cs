namespace Innovayse.Application.Admin.Servers.Commands.UpdateServer;
using Innovayse.Application.Common;
using Innovayse.Domain.Servers.Interfaces;

/// <summary>
/// Handles <see cref="UpdateServerCommand"/> by finding and updating the target server.
/// </summary>
public sealed class UpdateServerHandler(IServerRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Updates the server, throwing if not found.
    /// </summary>
    /// <param name="cmd">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when no server with the given ID exists.</exception>
    public async Task HandleAsync(UpdateServerCommand cmd, CancellationToken ct)
    {
        var server = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Server {cmd.Id} not found.");

        server.Update(cmd.Details);
        await uow.SaveChangesAsync(ct);
    }
}
