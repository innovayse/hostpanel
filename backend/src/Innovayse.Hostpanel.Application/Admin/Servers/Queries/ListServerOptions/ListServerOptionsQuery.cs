using Innovayse.Hostpanel.Domain.Servers.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Queries.ListServerOptions;

/// <summary>
/// Returns every provisioning server as a lean option, by name, for the screens that choose a server or summarise all of
/// them (the group form, the add existing service dialog, the service detail, the CWP 7 integration and the dashboard).
/// It is not paged on purpose: a picker needs the whole set at once. The server list screen reads
/// <c>ListServersQuery</c> instead.
/// </summary>
/// <param name="Module">Optional control panel module filter. Null returns every module.</param>
public sealed record ListServerOptionsQuery(ServerModule? Module = null);
