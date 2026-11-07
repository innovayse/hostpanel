using Hostpanel.Sdk.Plugins.Enums;

namespace Innovayse.Hostpanel.Application.Plugins.Queries.GetPluginUi;

/// <summary>Asks for the plugin menu entries and pages of one panel that the caller may open.</summary>
/// <param name="Area">The panel: client or admin.</param>
public sealed record GetPluginUiQuery(PluginArea Area);
