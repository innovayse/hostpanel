using Innovayse.Hostpanel.Application.Admin.Servers.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Maps a server's control panel module id to the installed plugin that serves it for one kind of work (a hosting panel today, a
/// virtual server platform next). The one reader of the plugins' <c>serverModules</c> lists for calls; it never activates a plugin,
/// so a plugin that is off still owns its module and the caller answers that it is unavailable.
/// </summary>
public interface IServerModulePluginResolver
{
    /// <summary>Finds the plugin that serves a module for a capability.</summary>
    /// <param name="module">The server's module id, matched ignoring case.</param>
    /// <param name="capability">The capability the work needs, a <c>PluginCapabilities</c> name; only plugins that declare it count.</param>
    /// <returns>No plugin, the one plugin, or a conflict when several declare the module for that capability.</returns>
    public ServerModulePluginMatch Find(string module, string capability);
}
