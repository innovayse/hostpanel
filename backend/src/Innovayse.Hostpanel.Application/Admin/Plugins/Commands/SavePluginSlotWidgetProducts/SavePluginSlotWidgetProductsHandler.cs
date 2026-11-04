using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Plugins;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.SavePluginSlotWidgetProducts;

/// <summary>
/// Handles <see cref="SavePluginSlotWidgetProductsCommand"/>: checks the widget is one the plugin declares, drops the
/// unknown and deleted products, and makes the widget rows of <c>PluginSlotProductBindings</c> equal to what is left,
/// without touching the rows of other widgets. Saves of one widget run one after another (a database lock per plugin,
/// slot and widget held for the transaction), so the last save wins whole and two saves of disjoint sets never merge.
/// The table is written nowhere else.
/// </summary>
public sealed class SavePluginSlotWidgetProductsHandler
{
    /// <summary>
    /// How many times the save is tried. The lock serialises the saves, but a first binding can still be written by a
    /// writer that does not take it; the unique index then refuses the second insert, and one more attempt reads the
    /// winner's rows and reconciles with them.
    /// </summary>
    private const int MaxAttempts = 2;

    /// <summary>The plugins loaded at startup and their manifests.</summary>
    private readonly IPluginRegistry _registry;

    /// <summary>Tells which of the submitted products exist.</summary>
    private readonly IPluginSlotContextReader _context;

    /// <summary>The module data context: loads and saves the binding rows.</summary>
    private readonly IHostpanelDbContext _db;

    /// <summary>Initializes the handler.</summary>
    /// <param name="registry">The plugins loaded at startup and their manifests.</param>
    /// <param name="context">Tells which of the submitted products exist.</param>
    /// <param name="db">The module data context: loads and saves the binding rows.</param>
    public SavePluginSlotWidgetProductsHandler(IPluginRegistry registry, IPluginSlotContextReader context, IHostpanelDbContext db)
    {
        _registry = registry;
        _context = context;
        _db = db;
    }

    /// <summary>Stores the widget product binding.</summary>
    /// <param name="command">The widget and its products.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The widget with the products that were stored (the existing ones among those sent); <c>PLUGIN_NOT_FOUND</c> when no
    /// loaded plugin has that id; <c>PLUGIN_SLOT_WIDGET_NOT_FOUND</c> when the plugin declares no such widget in that slot;
    /// <c>PLUGIN_SLOT_PRODUCTS_UNKNOWN</c> when products were sent but none of them exists (the binding is left as it was,
    /// because only an empty list clears it).
    /// </returns>
    /// <exception cref="ConcurrencyConflictException">Thrown when a competing save won the race on every attempt.</exception>
    public async Task<Result<PluginSlotWidgetBindingDto>> HandleAsync(SavePluginSlotWidgetProductsCommand command, CancellationToken ct)
    {
        var manifest = _registry.GetLoadedManifests().FirstOrDefault(m => string.Equals(m.Id, command.PluginId, StringComparison.Ordinal));
        if (manifest is null)
        {
            return Result<PluginSlotWidgetBindingDto>.FailureCode(HostpanelErrorCodes.PLUGIN_NOT_FOUND);
        }

        var entry = manifest.Ui.Slots.FirstOrDefault(
            e => string.Equals(e.Slot, command.SlotId, StringComparison.Ordinal) && string.Equals(e.Id, command.WidgetId, StringComparison.Ordinal));
        if (entry is null)
        {
            return Result<PluginSlotWidgetBindingDto>.FailureCode(HostpanelErrorCodes.PLUGIN_SLOT_WIDGET_NOT_FOUND);
        }

        var requested = (command.ProductIds ?? []).Distinct().ToList();

        IReadOnlyList<int>? kept;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                kept = await ReplaceAsync(command, requested, ct);
                break;
            }
            catch (ConcurrencyConflictException) when (attempt < MaxAttempts)
            {
                // The competing save committed first and this attempt rolled back whole. Nothing else is tracked by
                // this scope, so everything is dropped; the next attempt reads the products and the rows again.
                _db.DetachAll();
            }
        }

        if (kept is null)
        {
            return Result<PluginSlotWidgetBindingDto>.FailureCode(HostpanelErrorCodes.PLUGIN_SLOT_PRODUCTS_UNKNOWN);
        }

        return Result<PluginSlotWidgetBindingDto>.Success(new PluginSlotWidgetBindingDto(
            entry.Slot, entry.Id, PluginText.Resolve(manifest, entry.Title), entry.ProductIds.Order().ToList(), kept, kept.Count > 0));
    }

    /// <summary>
    /// Makes the stored rows of the widget equal the existing products among <paramref name="requested"/> in one
    /// transaction that holds the widget's lock, so the products are checked and the rows read after any earlier save of
    /// the widget has committed.
    /// </summary>
    /// <param name="command">The widget.</param>
    /// <param name="requested">The products the widget is to be bound to; empty removes the binding.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The products that were stored, ordered; <see langword="null"/> when products were requested and none exists, in which
    /// case nothing was written.
    /// </returns>
    /// <exception cref="ConcurrencyConflictException">Thrown when a competing save wrote one of the rows first.</exception>
    private async Task<IReadOnlyList<int>?> ReplaceAsync(
        SavePluginSlotWidgetProductsCommand command, IReadOnlyList<int> requested, CancellationToken ct)
    {
        await using var transaction = await _db.BeginTransactionAsync(ct);
        await _db.LockForTransactionAsync($"PluginSlotWidget:{command.PluginId}:{command.SlotId}:{command.WidgetId}", ct);

        // Read again on every attempt: a product may have been deleted since the last one.
        var products = (await _context.ExistingProductIdsAsync(requested, ct)).Order().ToList();
        if (requested.Count > 0 && products.Count == 0)
        {
            return null;
        }

        // Tracked: rows no longer wanted are removed below. Rows of a deleted product are included, so they are cleaned up too.
        var stored = await _db.PluginSlotProductBindings
            .Where(b => b.PluginId == command.PluginId && b.SlotId == command.SlotId && b.WidgetId == command.WidgetId)
            .ToListAsync(ct);

        foreach (var row in stored.Where(row => !products.Contains(row.ProductId)))
        {
            _db.PluginSlotProductBindings.Remove(row);
        }

        var present = stored.Select(row => row.ProductId).ToHashSet();
        foreach (var productId in products.Where(id => !present.Contains(id)))
        {
            _db.PluginSlotProductBindings.Add(
                PluginSlotProductBinding.Create(command.PluginId, command.SlotId, command.WidgetId, productId));
        }

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return products;
    }
}
