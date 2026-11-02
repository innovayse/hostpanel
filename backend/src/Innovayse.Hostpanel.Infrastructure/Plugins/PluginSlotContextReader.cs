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
}
