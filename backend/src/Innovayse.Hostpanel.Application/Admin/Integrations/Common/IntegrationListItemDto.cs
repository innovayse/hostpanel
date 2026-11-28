using Hostpanel.Sdk.Plugins.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common;

/// <summary>
/// Summary row returned by the list-integrations endpoint.
/// </summary>
/// <param name="Slug">URL-safe identifier for the integration (e.g. "card").</param>
/// <param name="Category">Grouping label (e.g. "Payment Gateways").</param>
/// <param name="IsEnabled">Whether the integration is currently active.</param>
/// <param name="IsConfigured">Whether all required credential fields are non-empty.</param>
/// <param name="IsPlugin">True if this entry comes from an installed plugin (not built-in).</param>
/// <param name="PluginType">
/// The plugin type its manifest declares, for an entry that comes from a plugin; <see langword="null"/> for a
/// built-in integration. The admin groups fulfilment, identity and hosted plugins by it, since no built-in
/// category holds them.
/// </param>
/// <param name="Connection">
/// The last known connection state of a plugin that implements <c>IPluginStatus</c> and is switched on, cached for
/// <c>PluginApi:StatusCacheSeconds</c>; <see langword="null"/> for a built-in integration and for any other plugin.
/// </param>
/// <param name="IconUrl">
/// Path of the admin icon endpoint (<c>/api/admin/plugins/{id}/icon?v={version}</c>, the plugin version in the address so an update is never shown the cached old icon) for a plugin entry that ships an icon;
/// <see langword="null"/> for a built-in integration and for a plugin without an icon.
/// </param>
/// <param name="Description">
/// The plugin's short description in the request language (a manifest locale key resolved, else the plain text);
/// <see langword="null"/> for a built-in integration, whose card text the admin owns, and for a plugin whose manifest is not loaded.
/// </param>
/// <param name="Name">Display name: the manifest name for a plugin; left out for a built-in integration, whose name the admin owns and resolves from <paramref name="Slug"/>.</param>
public sealed record IntegrationListItemDto(
    string Slug,
    string Category,
    bool IsEnabled,
    bool IsConfigured,
    bool IsPlugin = false,
    PluginType? PluginType = null,
    IntegrationConnectionDto? Connection = null,
    string? IconUrl = null,
    string? Description = null,
    string? Name = null);
