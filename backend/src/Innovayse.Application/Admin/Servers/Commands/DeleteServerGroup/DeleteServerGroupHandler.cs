namespace Innovayse.Application.Admin.Servers.Commands.DeleteServerGroup;
using Innovayse.Application.Common;
using Innovayse.Domain.Servers.Interfaces;

/// <summary>
/// Handles <see cref="DeleteServerGroupCommand"/> by removing the group (servers are unassigned via FK cascade rule).
/// </summary>
public sealed class DeleteServerGroupHandler(IServerGroupRepository groupRepo, IUnitOfWork uow)
{
    /// <summary>
    /// Deletes the group, throwing if not found.
    /// </summary>
    /// <param name="cmd">The delete command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the group is not found.</exception>
    public async Task HandleAsync(DeleteServerGroupCommand cmd, CancellationToken ct)
    {
        var group = await groupRepo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"ServerGroup {cmd.Id} not found.");

        groupRepo.Remove(group);
        await uow.SaveChangesAsync(ct);
    }
}
