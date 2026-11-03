using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>Reads the service, client and setting rows a plugin slot index depends on.</summary>
public sealed class PluginSlotContextReader : IPluginSlotContextReader
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _db;

    /// <summary>Initializes the reader.</summary>
    /// <param name="db">The module's data context.</param>
    public PluginSlotContextReader(IHostpanelDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public Task<PluginSlotServiceDto?> FindServiceAsync(int serviceId, CancellationToken ct)
    {
        return _db.ClientServices.AsNoTracking()
            .Where(s => s.Id == serviceId)
            .Select(s => new PluginSlotServiceDto(s.ClientId, s.ProductId))
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc/>
    public Task<bool> ClientExistsAsync(int clientId, CancellationToken ct)
    {
        return _db.Clients.AsNoTracking().AnyAsync(c => c.Id == clientId, ct);
    }

    /// <inheritdoc/>
    public Task<string?> ReadHiddenWidgetsAsync(CancellationToken ct)
    {
        return _db.Settings.AsNoTracking()
            .Where(s => s.Key == PluginSettingKeys.HiddenSlotWidgets)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PluginSlotBindingDto>> ListBindingsAsync(string? pluginId, string? slotId, CancellationToken ct)
    {
        // A left join: the row of a deleted product stays, flagged, so a bound widget is never mistaken for an unbound one.
        return await (
            from b in _db.PluginSlotProductBindings.AsNoTracking()
            where (pluginId == null || b.PluginId == pluginId) && (slotId == null || b.SlotId == slotId)
            join p in _db.Products.AsNoTracking() on b.ProductId equals p.Id into products
            select new PluginSlotBindingDto(b.PluginId, b.SlotId, b.WidgetId, b.ProductId, products.Any()))
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlySet<int>> ExistingProductIdsAsync(IReadOnlyCollection<int> productIds, CancellationToken ct)
    {
        if (productIds.Count == 0)
        {
            return new HashSet<int>();
        }

        var existing = await _db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(ct);
        return existing.ToHashSet();
    }
}
