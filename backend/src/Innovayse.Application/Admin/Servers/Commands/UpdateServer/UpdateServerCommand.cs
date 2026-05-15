namespace Innovayse.Application.Admin.Servers.Commands.UpdateServer;
using Innovayse.Domain.Servers;

/// <summary>
/// Updates an existing provisioning server's full configuration.
/// </summary>
/// <param name="Id">Identifier of the server to update.</param>
/// <param name="Details">Updated configuration fields.</param>
public record UpdateServerCommand(int Id, ServerDetails Details);
