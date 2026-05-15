namespace Innovayse.Application.Admin.Servers.Queries.ListServers;
using Innovayse.Domain.Servers;

/// <summary>
/// Returns all provisioning servers, optionally filtered by module type.
/// </summary>
/// <param name="Module">Optional module filter. Null returns all servers.</param>
public record ListServersQuery(ServerModule? Module);
