namespace Innovayse.Application.Admin.Servers.Commands.UpdateServerGroup;
using Innovayse.Domain.Servers;

/// <summary>
/// Updates an existing server group's name, fill type, and server membership.
/// </summary>
/// <param name="Id">The group to update.</param>
/// <param name="Name">New display name.</param>
/// <param name="FillType">New fill strategy.</param>
/// <param name="ServerIds">Complete new list of server IDs for this group.</param>
public record UpdateServerGroupCommand(
    int Id,
    string Name,
    ServerFillType FillType,
    List<int> ServerIds);
