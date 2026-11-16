using Innovayse.Hostpanel.Application.Admin.Servers.Common.Enums;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Servers.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Extensions;

/// <summary>
/// The server filters the paged server list and the unpaged server options share, so the module and the state are
/// narrowed in one place and a list and a picker never disagree about what "Online" means.
/// </summary>
public static class ServerQueryableExtensions
{
    /// <summary>Keeps the servers that run the module and are in the state; a filter left out lets every server through.</summary>
    /// <param name="servers">The servers to narrow.</param>
    /// <param name="module">The control panel module the server must run, or null for any.</param>
    /// <param name="state">The last known connection state the server must be in, or null for any.</param>
    /// <returns>The narrowed query.</returns>
    public static IQueryable<Server> Matching(this IQueryable<Server> servers, ServerModule? module, ServerState? state = null)
    {
        ArgumentNullException.ThrowIfNull(servers);

        if (module is { } wanted)
        {
            servers = servers.Where(s => s.Module == wanted);
        }

        return state switch
        {
            ServerState.Online => servers.Where(s => s.IsOnline == true),
            ServerState.Offline => servers.Where(s => s.IsOnline == false),
            ServerState.Untested => servers.Where(s => s.IsOnline == null),
            _ => servers,
        };
    }
}
