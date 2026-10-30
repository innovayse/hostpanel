using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Admin.Integrations.Queries.ListIntegrations;

/// <summary>
/// Handles <see cref="ListIntegrationsQuery"/> by reading all integration-prefixed settings and
/// computing the enabled/configured status for each known integration, then merging in any plugins
/// registered via <see cref="IPluginIntegrationRegistry"/>.
/// </summary>
public sealed class ListIntegrationsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Plugin integration registry for discovering plugin-provided integrations.</summary>
    private readonly IPluginIntegrationRegistry _pluginRegistry;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="pluginRegistry">Plugin integration registry for discovering plugin-provided integrations.</param>
    public ListIntegrationsHandler(IHostpanelDbContext context, IPluginIntegrationRegistry pluginRegistry)
    {
        _context = context;
        _pluginRegistry = pluginRegistry;
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

            var isConfigured = plugin.FieldDefinitions
                .Where(f => f.Required)
                .All(f => settings.HasValue(plugin.Slug, f.Key));

            result.Add(new IntegrationListItemDto(
                Slug: plugin.Slug,
                Name: plugin.Name,
                Category: plugin.Category,
                IsEnabled: isEnabled,
                IsConfigured: isConfigured,
                IsPlugin: true,
                PluginType: plugin.Type));
        }

        return Result<IReadOnlyList<IntegrationListItemDto>>.Success(result);
    }
}
