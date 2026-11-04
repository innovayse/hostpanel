namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>One plugin page in a panel's index.</summary>
/// <param name="PluginId">The plugin id.</param>
/// <param name="Id">The page id, unique within the plugin.</param>
/// <param name="Path">The page path below the plugin's own prefix in the panel, for example <c>/sites/{serviceId}</c>.</param>
/// <param name="Title">The title, in the caller's language.</param>
public sealed record PluginUiPageDto(string PluginId, string Id, string Path, string Title);
