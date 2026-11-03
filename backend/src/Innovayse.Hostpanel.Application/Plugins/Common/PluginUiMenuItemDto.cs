namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>One plugin menu entry of a panel; it is listed only beside a page the caller may open.</summary>
/// <param name="PluginId">The plugin id.</param>
/// <param name="Id">The entry id, unique within the plugin.</param>
/// <param name="Label">The label, in the caller's language.</param>
/// <param name="Page">The id of the page the entry opens, one of the same plugin's <see cref="PluginUiDto.Pages"/>.</param>
/// <param name="Icon">The icon name, or <see langword="null"/> for the panel's default icon.</param>
/// <param name="Order">The position among the entries, lowest first.</param>
public sealed record PluginUiMenuItemDto(string PluginId, string Id, string Label, string Page, string? Icon, int Order);
