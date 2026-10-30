using Innovayse.SDK.Plugins.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common;

/// <summary>
/// Summary row returned by the list-integrations endpoint.
/// </summary>
/// <param name="Slug">URL-safe identifier for the integration (e.g. "card").</param>
/// <param name="Name">Human-readable display name (e.g. "Card gateway").</param>
/// <param name="Category">Grouping label (e.g. "Payment Gateways").</param>
/// <param name="IsEnabled">Whether the integration is currently active.</param>
/// <param name="IsConfigured">Whether all required credential fields are non-empty.</param>
/// <param name="IsPlugin">True if this entry comes from an installed plugin (not built-in).</param>
/// <param name="PluginType">
/// The plugin type its manifest declares, for an entry that comes from a plugin; <see langword="null"/> for a
/// built-in integration. The admin groups fulfilment, identity and hosted plugins by it, since no built-in
/// category holds them.
/// </param>
public sealed record IntegrationListItemDto(
    string Slug,
    string Name,
    string Category,
    bool IsEnabled,
    bool IsConfigured,
    bool IsPlugin = false,
    PluginType? PluginType = null);
