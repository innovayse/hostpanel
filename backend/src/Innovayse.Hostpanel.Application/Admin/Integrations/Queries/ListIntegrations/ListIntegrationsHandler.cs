using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Admin.Integrations.Queries.ListIntegrations;

/// <summary>
/// Handles <see cref="ListIntegrationsQuery"/> by reading all integration-prefixed settings and
/// computing the enabled/configured status for each known integration, then merging in any plugins
/// registered via <see cref="IPluginIntegrationRegistry"/>, each switched-on plugin that implements <c>IPluginStatus</c>
/// with its cached connection state.
/// </summary>
public sealed class ListIntegrationsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Plugin integration registry for discovering plugin-provided integrations.</summary>
    private readonly IPluginIntegrationRegistry _pluginRegistry;

    /// <summary>The loaded plugins' manifests, whose locales word a plugin's connection state.</summary>
    private readonly IPluginRegistry _plugins;

    /// <summary>The cached connection state of a plugin that implements <c>IPluginStatus</c>.</summary>
    private readonly PluginConnectionReader _connections;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="pluginRegistry">Plugin integration registry for discovering plugin-provided integrations.</param>
    /// <param name="plugins">The loaded plugins' manifests, whose locales word a plugin's connection state.</param>
    /// <param name="connections">The cached connection state of a plugin that implements <c>IPluginStatus</c>.</param>
    public ListIntegrationsHandler(
        IHostpanelDbContext context,
        IPluginIntegrationRegistry pluginRegistry,
        IPluginRegistry plugins,
        PluginConnectionReader connections)
    {
        _context = context;
        _pluginRegistry = pluginRegistry;
        _plugins = plugins;
        _connections = connections;
    }

    /// <summary>
    /// Returns one <see cref="IntegrationListItemDto"/> per known integration.
    /// </summary>
    /// <param name="query">The list integrations query (no parameters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Every known integration and every plugin-provided one, always succeeding.</returns>
    public async Task<Result<IReadOnlyList<IntegrationListItemDto>>> HandleAsync(
        ListIntegrationsQuery query, CancellationToken ct)
    {
        var settings = await IntegrationSettingsLookup.LoadAsync(_context, IntegrationSettingKeys.RootPrefix, ct);

        var result = new List<IntegrationListItemDto>(IntegrationCatalogue.All.Count);

        foreach (var (slug, entry) in IntegrationCatalogue.All)
        {
            var isEnabled = settings.IsEnabled(slug);

            var isConfigured = entry.RequiredFields.All(field => settings.HasValue(slug, field));

            result.Add(new IntegrationListItemDto(slug, entry.Name, entry.Category, isEnabled, isConfigured));
        }

        foreach (var plugin in _pluginRegistry.GetAll())
        {
            if (IntegrationCatalogue.All.ContainsKey(plugin.Slug))
            {
                continue;
            }

            var isEnabled = settings.IsEnabled(plugin.Slug);

            var isConfigured = plugin.RequiredSettingKeys.All(key => settings.HasValue(plugin.Slug, key));

            // Only a switched-on plugin is asked: a switched-off one would only answer "inactive", which its
            // card already shows, and asking it would run plugin code on every list load for nothing.
            var manifest = isEnabled ? _plugins.GetLoadedManifests().FirstOrDefault(m => m.Id == plugin.Slug) : null;
            var connection = manifest is null ? null : await _connections.ReadAsync(manifest, ct);

            result.Add(new IntegrationListItemDto(
                Slug: plugin.Slug,
                Name: plugin.Name,
                Category: plugin.Category,
                IsEnabled: isEnabled,
                IsConfigured: isConfigured,
                IsPlugin: true,
                PluginType: plugin.Type,
                Connection: connection,
                IconUrl: plugin.IconUrl));
        }

        return Result<IReadOnlyList<IntegrationListItemDto>>.Success(result);
    }
}
