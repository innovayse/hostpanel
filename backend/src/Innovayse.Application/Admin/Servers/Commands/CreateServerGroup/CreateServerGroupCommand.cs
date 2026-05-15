namespace Innovayse.Application.Admin.Servers.Commands.CreateServerGroup;
using Innovayse.Domain.Servers;

/// <summary>
/// Creates a new server group and optionally assigns existing servers to it.
/// </summary>
/// <param name="Name">Display name for the group.</param>
/// <param name="FillType">Account distribution strategy.</param>
/// <param name="ServerIds">IDs of servers to add to this group.</param>
public record CreateServerGroupCommand(
    string Name,
    ServerFillType FillType,
    List<int> ServerIds);
