using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Manifest;
using Innovayse.SDK.Plugins.Slots;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// Lists the widgets enabled plugins put into one slot, the part the client and the admin slot index share. It reads
/// manifests and settings only: no plugin is built or called, so the caller's context must already be verified by the
/// area's own handler before this runs, and nothing it returns depends on what a plugin would answer.
/// </summary>
/// <remarks>
/// A widget is listed when its plugin is an app plugin that is loaded, switched on and configured (the same gates a
/// plugin route passes, <see cref="IPluginEndpointResolver"/>), the operator has not hidden it
/// (<see cref="PluginSettingKeys.HiddenSlotWidgets"/>), the viewer holds its permission (client slots) and the slot's
/// service is one of the products it is bound to (the operator's binding when the widget was ever bound, even if
/// every product of it was deleted since, which shows it nowhere; else the manifest <c>productIds</c>; a widget with
/// neither is for every product).
/// </remarks>
public sealed class PluginSlotIndexBuilder
{
    /// <summary>The plugins loaded at startup and their manifests.</summary>
    private readonly IPluginRegistry _registry;

    /// <summary>Finds an app plugin and checks that it is switched on, without building it.</summary>
    private readonly IPluginEndpointResolver _plugins;

    /// <summary>Reads the operator's hidden widgets setting.</summary>
    private readonly IPluginSlotContextReader _context;

    /// <summary>Initializes a new instance of the <see cref="PluginSlotIndexBuilder"/> class.</summary>
    /// <param name="registry">The plugins loaded at startup and their manifests.</param>
    /// <param name="plugins">Finds an app plugin and checks that it is switched on, without building it.</param>
    /// <param name="context">Reads the operator's hidden widgets setting.</param>
    public PluginSlotIndexBuilder(IPluginRegistry registry, IPluginEndpointResolver plugins, IPluginSlotContextReader context)
    {
        _registry = registry;
        _plugins = plugins;
        _context = context;
    }

    /// <summary>Builds the index of a slot for a verified context.</summary>
    /// <param name="slot">The slot, of the area the caller is in.</param>
    /// <param name="held">The client permissions the viewer holds; not looked at for an admin slot, which takes none.</param>
    /// <param name="productId">The product of the slot's service; <see langword="null"/> when the slot has no service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The widgets to show, ordered by <c>order</c>, then plugin id, then widget id; empty when there are none.</returns>
    public async Task<PluginSlotIndexDto> BuildAsync(PluginSlot slot, ClientPermission held, int? productId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(slot);

        var hiddenValue = await _context.ReadHiddenWidgetsAsync(ct);
        var hidden = PluginSettingKeys.HiddenWidgets(hiddenValue);

        // Read once per slot: every stored operator binding row of its widgets, rows of deleted products included, so a
        // widget the operator bound is told apart from one never bound.
        var bound = (await _context.ListBindingsAsync(null, slot.Id, ct))
            .ToLookup(binding => (binding.PluginId, binding.WidgetId));

        var widgets = new List<PluginSlotWidgetDto>();
        foreach (var manifest in _registry.GetLoadedManifests())
        {
            var entries = manifest.Ui.Slots
                .Where(entry => entry.Slot == slot.Id
                    && !hidden.Contains(PluginSettingKeys.HiddenWidgetEntry(slot.Id, manifest.Id, entry.Id))
                    && (slot.Area != PluginArea.Client || PluginClientPermissions.IsHeld(held, entry.Permission))
                    && IsBoundTo(entry, bound[(manifest.Id, entry.Id)].ToList(), productId))
                .ToList();

            // The switch is read only for a plugin that has something to show, and nothing of the plugin is built.
            if (entries.Count == 0 || _plugins.Find(manifest.Id) is null || await _plugins.PrepareAsync(manifest.Id, ct) is null)
            {
                continue;
            }

            widgets.AddRange(entries.Select(entry => new PluginSlotWidgetDto(
                manifest.Id, entry.Id, entry.View, PluginText.Resolve(manifest, entry.Title), entry.Order, entry.ContextKeys)));
        }

        var ordered = widgets
            .OrderBy(widget => widget.Order)
            .ThenBy(widget => widget.PluginId, StringComparer.Ordinal)
            .ThenBy(widget => widget.Id, StringComparer.Ordinal)
            .ToList();

        return new PluginSlotIndexDto(slot.Id, ordered);
    }

    /// <summary>Whether a widget may be shown for the slot's product.</summary>
    /// <param name="entry">The widget.</param>
    /// <param name="operatorRows">The binding rows the operator stored for the widget, of deleted products too; empty when it was never bound.</param>
    /// <param name="productId">The product of the slot's service, or <see langword="null"/> when the slot has none.</param>
    /// <returns>
    /// For a widget the operator bound: only when <paramref name="productId"/> is one of its existing products, so a widget
    /// whose products were all deleted shows nowhere. For a widget never bound: the manifest binding applies, and a widget
    /// with none is for every product. A bound widget never shows where there is no service to check.
    /// </returns>
    private static bool IsBoundTo(PluginSlotEntry entry, IReadOnlyList<PluginSlotBindingDto> operatorRows, int? productId)
    {
        // Fail closed: the binding feeds access decisions, so a binding that lost its products must not widen to the manifest.
        if (operatorRows.Count > 0)
        {
            return productId is { } bound && operatorRows.Any(row => row.ProductExists && row.ProductId == bound);
        }

        IReadOnlyCollection<int> products = entry.ProductIds;
        return products.Count == 0 || (productId is { } product && products.Contains(product));
    }
}
