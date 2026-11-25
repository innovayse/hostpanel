using Innovayse.Hostpanel.Application.Services.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Names the panel plugin that serves a server's module, for the screens that show it next to a service. It only reads the plugins'
/// manifests through <see cref="IServerModulePluginResolver"/>; no plugin is activated or called.
/// </summary>
public interface IServerPanelPluginReader
{
    /// <summary>Finds the panel plugin that serves a module.</summary>
    /// <param name="module">The server's module id, matched ignoring case.</param>
    /// <returns>The serving plugin, or <see langword="null"/> when none serves the module (a module the host provisions itself, or two plugins claiming it).</returns>
    public ServicePanelPluginDto? Find(string module);
}
