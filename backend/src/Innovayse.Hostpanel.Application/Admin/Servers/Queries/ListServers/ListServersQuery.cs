using Innovayse.Hostpanel.Domain.Servers.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Queries.ListServers;

/// <summary>
/// Returns all provisioning servers, optionally filtered by module type.
/// </summary>
/// <param name="Module">Optional module filter. Null returns all servers.</param>
public sealed record ListServersQuery(ServerModule? Module);
