using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Common.Enums;
using Innovayse.Hostpanel.Domain.Servers;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Extensions;

/// <summary>
/// The server filters the paged server list, the unpaged server options, the server selector and the disk usage report
/// share, so the module and the state are narrowed in one place and a list and a picker never disagree about what
/// "Online" means or about which servers run a module.
/// </summary>
public static class ServerQueryableExtensions
{
    /// <summary>Keeps the servers that run the module and are in the state; a filter left out lets every server through.</summary>
    /// <param name="servers">The servers to narrow.</param>
    /// <param name="module">The control panel module id the server must run, matched ignoring case, or null for any.</param>
    /// <param name="state">The last known connection state the server must be in, or null for any.</param>
    /// <returns>The narrowed query.</returns>
    public static IQueryable<Server> Matching(this IQueryable<Server> servers, string? module, ServerState? state = null)
    {
        ArgumentNullException.ThrowIfNull(servers);

        if (module is not null)
        {
            servers = servers.OfModules([module]);
        }

        return state switch
        {
            ServerState.Online => servers.Where(s => s.IsOnline == true),
            ServerState.Offline => servers.Where(s => s.IsOnline == false),
            ServerState.Untested => servers.Where(s => s.IsOnline == null),
            _ => servers,
        };
    }

    /// <summary>
    /// Keeps the servers that run any of the modules. Both sides are lower cased in SQL, so a row stored as <c>DirectAdmin</c> matches
    /// the plugin id <c>directadmin</c>: <see cref="ServerModules.Same"/> in a query.
    /// </summary>
    /// <param name="servers">The servers to narrow.</param>
    /// <param name="modules">The control panel module ids a server may run.</param>
    /// <returns>The narrowed query.</returns>
    public static IQueryable<Server> OfModules(this IQueryable<Server> servers, IReadOnlyCollection<string> modules)
    {
        ArgumentNullException.ThrowIfNull(servers);
        ArgumentNullException.ThrowIfNull(modules);

        var wanted = modules.Select(module => module.ToLowerInvariant()).ToList();
        return servers.Where(s => wanted.Contains(s.Module.ToLower()));
    }

    /// <summary>
    /// Projects the servers to <see cref="ServerOptionDto"/> in SQL, so no credential or hosting column is read. The one projection of
    /// the lean server read, shared by the option lookup and the plugin settings page.
    /// </summary>
    /// <param name="servers">The servers to project, already narrowed and ordered by the caller.</param>
    /// <returns>The projected query.</returns>
    public static IQueryable<ServerOptionDto> ToOptions(this IQueryable<Server> servers)
    {
        ArgumentNullException.ThrowIfNull(servers);

        return servers.Select(s => new ServerOptionDto(
            s.Id,
            s.Name,
            s.Hostname,
            s.IpAddress,
            s.IsOnline,
            s.LastTestedAt,
            s.AccountsCount,
            new ServerLimitsDto(s.MaxAccounts, s.IsDefault, s.IsDisabled)));
    }
}
