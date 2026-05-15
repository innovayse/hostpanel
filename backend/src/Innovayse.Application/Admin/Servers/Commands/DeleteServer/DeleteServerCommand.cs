namespace Innovayse.Application.Admin.Servers.Commands.DeleteServer;

/// <summary>
/// Deletes a provisioning server by identifier.
/// </summary>
/// <param name="Id">The server to delete.</param>
public record DeleteServerCommand(int Id);
