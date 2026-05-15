namespace Innovayse.Application.Admin.Servers.Commands.CreateServerGroup;
using Innovayse.Application.Common;
using Innovayse.Domain.Servers;
using Innovayse.Domain.Servers.Interfaces;

/// <summary>
/// Handles <see cref="CreateServerGroupCommand"/> by creating the group and assigning servers.
/// </summary>
public sealed class CreateServerGroupHandler(
    IServerGroupRepository groupRepo,
    IServerRepository serverRepo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Creates the group, assigns requested servers, and returns the new group ID.
    /// </summary>
    /// <param name="cmd">The create command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new group's identifier.</returns>
    public async Task<int> HandleAsync(CreateServerGroupCommand cmd, CancellationToken ct)
    {
        var group = ServerGroup.Create(cmd.Name, cmd.FillType);
        groupRepo.Add(group);
        await uow.SaveChangesAsync(ct);

        foreach (var serverId in cmd.ServerIds)
        {
            var server = await serverRepo.FindByIdAsync(serverId, ct);
            server?.AssignToGroup(group.Id);
        }

        await uow.SaveChangesAsync(ct);
        return group.Id;
    }
}
