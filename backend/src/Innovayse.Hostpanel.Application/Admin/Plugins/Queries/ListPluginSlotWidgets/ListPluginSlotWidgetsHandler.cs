using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.ListPluginSlotWidgets;

/// <summary>
/// Handles <see cref="ListPluginSlotWidgetsQuery"/> from the plugin's manifest and the operator's product bindings. No
/// plugin is built or called.
/// </summary>
public sealed class ListPluginSlotWidgetsHandler
{
    /// <summary>The plugins loaded at startup and their manifests.</summary>
    private readonly IPluginRegistry _registry;

    /// <summary>Reads the operator bindings.</summary>
    private readonly IPluginSlotContextReader _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="registry">The plugins loaded at startup and their manifests.</param>
    /// <param name="context">Reads the operator bindings of existing products.</param>
    public ListPluginSlotWidgetsHandler(IPluginRegistry registry, IPluginSlotContextReader context)
    {
        _registry = registry;
        _context = context;
    }

    /// <summary>Lists the plugin's slot widgets with their current binding.</summary>
    /// <param name="query">The plugin.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The widgets in manifest order (empty when the plugin declares none), or <c>PLUGIN_NOT_FOUND</c> when no loaded plugin has that id.</returns>
    public async Task<Result<IReadOnlyList<PluginSlotWidgetBindingDto>>> HandleAsync(
        ListPluginSlotWidgetsQuery query, CancellationToken ct)
    {
        var manifest = _registry.GetLoadedManifests().FirstOrDefault(m => string.Equals(m.Id, query.PluginId, StringComparison.Ordinal));
        if (manifest is null)
        {
            return Result<IReadOnlyList<PluginSlotWidgetBindingDto>>.FailureCode(HostpanelErrorCodes.PLUGIN_NOT_FOUND);
        }

        var bindings = (await _context.ListBindingsAsync(manifest.Id, null, ct))
            .ToLookup(binding => (binding.SlotId, binding.WidgetId));
        var widgets = manifest.Ui.Slots
            .Select(entry =>
            {
                var rows = bindings[(entry.Slot, entry.Id)].ToList();
                return new PluginSlotWidgetBindingDto(
                    entry.Slot,
                    entry.Id,
                    PluginText.Resolve(manifest, entry.Title),
                    entry.ProductIds.Order().ToList(),
                    rows.Where(row => row.ProductExists).Select(row => row.ProductId).Order().ToList(),
                    rows.Count > 0);
            })
            .ToList();

        return Result<IReadOnlyList<PluginSlotWidgetBindingDto>>.Success(widgets);
    }
}
