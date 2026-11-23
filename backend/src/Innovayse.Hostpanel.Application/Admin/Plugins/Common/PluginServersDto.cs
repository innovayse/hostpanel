using Innovayse.Hostpanel.Application.Admin.Servers.Common;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// The servers a panel plugin serves, for the plugin's settings page: the server modules its manifest declares and, for each
/// server stored on one of them, the lean facts the Servers page shows. A plugin that serves no server module answers both lists empty.
/// </summary>
/// <param name="Modules">The <c>serverModules</c> the plugin declares, in the manifest's spelling; empty for a plugin that serves none.</param>
/// <param name="Servers">The servers stored on one of those modules (matched ignoring case), by name; empty when none is.</param>
public sealed record PluginServersDto(IReadOnlyList<string> Modules, IReadOnlyList<ServerOptionDto> Servers);
