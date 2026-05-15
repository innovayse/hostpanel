namespace Innovayse.Application.Admin.Servers.Commands.CreateServer;
using Innovayse.Domain.Servers;

/// <summary>
/// Creates a new provisioning server with full configuration.
/// </summary>
/// <param name="Details">All server configuration fields.</param>
public record CreateServerCommand(ServerDetails Details);
