namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>The plugin menu entries and pages of one panel that the caller may open, every label in the caller's language.</summary>
/// <param name="Menus">The menu entries, by <see cref="PluginUiMenuItemDto.Order"/>, then plugin id, then entry id.</param>
/// <param name="Pages">The pages, by plugin id, then the order the manifest declares them in.</param>
public sealed record PluginUiDto(IReadOnlyList<PluginUiMenuItemDto> Menus, IReadOnlyList<PluginUiPageDto> Pages);
