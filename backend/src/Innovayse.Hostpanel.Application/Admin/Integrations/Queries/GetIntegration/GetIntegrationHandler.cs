using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Hostpanel.Sdk.Plugins.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Integrations.Queries.GetIntegration;

/// <summary>
/// Handles <see cref="GetIntegrationQuery"/> by loading all settings for the given slug and
/// returning them alongside field definitions, with secret values withheld.
/// </summary>
public sealed class GetIntegrationHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Plugin registry for resolving plugin-provided integrations.</summary>
    private readonly IPluginRegistry _plugins;

    /// <summary>The cached connection state of a plugin that implements <c>IPluginStatus</c>.</summary>
    private readonly PluginConnectionReader _connections;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="plugins">Plugin registry for resolving plugin-provided integrations.</param>
    /// <param name="connections">The cached connection state of a plugin that implements <c>IPluginStatus</c>.</param>
    public GetIntegrationHandler(IHostpanelDbContext context, IPluginRegistry plugins, PluginConnectionReader connections)
    {
        _context = context;
        _plugins = plugins;
        _connections = connections;
    }

    /// <summary>
    /// Loads the configuration for the requested integration or plugin, withholds secret values, and
    /// returns field definitions for dynamic form rendering, whether it can be tested and, for a plugin that implements
    /// <c>IPluginStatus</c>, its cached connection state.
    /// </summary>
    /// <param name="query">Query containing the integration slug.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The integration detail with its field states and definitions, or
    /// <c>INTEGRATION_NOT_FOUND</c> when the slug names neither a built-in integration nor a
    /// loaded plugin.
    /// </returns>
    public async Task<Result<IntegrationDetailDto>> HandleAsync(GetIntegrationQuery query, CancellationToken ct)
    {
        var definition = IntegrationFields.Resolve(query.Slug, _plugins);
        if (definition is null)
        {
            return Result<IntegrationDetailDto>.FailureCode(HostpanelErrorCodes.INTEGRATION_NOT_FOUND);
        }

        var prefix = IntegrationSettingKeys.Prefix(query.Slug);
        var settings = await IntegrationSettingsLookup.LoadAsync(_context, prefix, ct);
        var isEnabled = settings.IsEnabled(query.Slug);

        var secrecy = SettingSecrecy.From(_plugins);
        var config = new Dictionary<string, IntegrationFieldValueDto>(definition.Fields.Count);
        foreach (var field in definition.Fields)
        {
            var key = IntegrationSettingKeys.FieldKey(query.Slug, field.Key);
            var value = settings.GetValue(query.Slug, field.Key);
            var hasValue = !string.IsNullOrWhiteSpace(value);

            // A secret is answered with whether it is set, never with its value or a mask of it.
            config[field.Key] = secrecy.IsSecret(key)
                ? new IntegrationFieldValueDto(null, IsSecret: true, hasValue)
                : new IntegrationFieldValueDto(value ?? string.Empty, IsSecret: false, hasValue);
        }

        // A built-in integration wins over a plugin of the same slug, as it does in the list.
        var isPlugin = !IntegrationCatalogue.All.ContainsKey(query.Slug) && _plugins.IsLoaded(query.Slug);
        var iconUrl = isPlugin && _plugins.GetIcon(query.Slug) is not null ? PluginIconPaths.For(query.Slug) : null;

        // A built-in slug the test knows keeps its own test. A plugin can be tested through IPluginStatus, or, for a
        // payment plugin, through the gateway status probe that Test Connection falls back to.
        var isBuiltIn = IntegrationCatalogue.TryGetTestable(query.Slug, out _);
        var manifest = isBuiltIn ? null : _plugins.GetLoadedManifests().FirstOrDefault(m => m.Id == query.Slug);
        var canTest = isBuiltIn
            || (manifest is not null && (_connections.Supports(manifest.Id) || manifest.Type == PluginType.Payment));
        var connection = manifest is not null && isEnabled ? await _connections.ReadAsync(manifest, ct) : null;

        return Result<IntegrationDetailDto>.Success(new IntegrationDetailDto(
            query.Slug, definition.Name, isEnabled, config, definition.Fields, canTest, connection, isPlugin, iconUrl));
    }
}
