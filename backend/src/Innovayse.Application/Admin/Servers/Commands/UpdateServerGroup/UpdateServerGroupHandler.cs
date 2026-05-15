namespace Innovayse.Application.Admin.Servers.Commands.UpdateServerGroup;
using Innovayse.Application.Common;
using Innovayse.Domain.Servers.Interfaces;

/// <summary>
/// Handles <see cref="UpdateServerGroupCommand"/> by updating the group and resyncing server membership.
/// </summary>
public sealed class UpdateServerGroupHandler(
    IServerGroupRepository groupRepo,
    IServerRepository serverRepo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Updates the group name and fill type, then resyncs which servers belong to it.
    /// Servers removed from the list have their group cleared; new servers are assigned.
    /// </summary>
    /// <param name="cmd">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the group is not found.</exception>
    public async Task HandleAsync(UpdateServerGroupCommand cmd, CancellationToken ct)
    {
        var group = await groupRepo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"ServerGroup {cmd.Id} not found.");

        group.Update(cmd.Name, cmd.FillType);

        // Remove servers no longer in the group
        foreach (var server in group.Servers.Where(s => !cmd.ServerIds.Contains(s.Id)).ToList())
        {
            server.RemoveFromGroup();
        }

        // Add newly assigned servers
        var currentIds = group.Servers.Select(s => s.Id).ToHashSet();
        foreach (var serverId in cmd.ServerIds.Where(id => !currentIds.Contains(id)))
        {
            var server = await serverRepo.FindByIdAsync(serverId, ct);
            server?.AssignToGroup(group.Id);
        }

        await uow.SaveChangesAsync(ct);
    }
}
