using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Innovayse.Hostpanel.Application.Domains.Commands.UpdateTldConfig;

/// <summary>
/// Handles <see cref="UpdateTldConfigCommand"/> by loading the existing TLD configuration,
/// applying field and pricing updates, and persisting the changes.
/// Invalidates the TLD pricing cache after updates to ensure clients see fresh prices.
/// </summary>
public sealed class UpdateTldConfigHandler
{
    /// <summary>The data context the configuration is loaded from and saved through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Memory cache for invalidating pricing data.</summary>
    private readonly IMemoryCache _cache;

    /// <summary>Tells which registrar plugins are installed.</summary>
    private readonly IRegistrarModuleCatalogue _modules;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context the configuration is loaded from and saved through.</param>
    /// <param name="cache">Memory cache for invalidating pricing data.</param>
    /// <param name="modules">Tells which registrar plugins are installed.</param>
    public UpdateTldConfigHandler(IHostpanelDbContext context, IMemoryCache cache, IRegistrarModuleCatalogue modules)
    {
        _context = context;
        _cache = cache;
        _modules = modules;
    }

    /// <summary>
    /// Updates an existing TLD configuration's settings, cost prices, and sell prices.
    /// </summary>
    /// <param name="cmd">The update TLD configuration command containing the new field values and pricing.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the changes are persisted, or <c>TLD_CONFIG_NOT_FOUND</c> (404) when the id
    /// names no configuration, or <c>TLD_REGISTRAR_MODULE_UNKNOWN</c> (400) when the module names
    /// no installed registrar plugin.
    /// </returns>
    public async Task<Result> HandleAsync(UpdateTldConfigCommand cmd, CancellationToken ct)
    {
        var entity = await _context.TldConfigs.FirstOrDefaultAsync(x => x.Id == cmd.Id!.Value, ct);
        if (entity is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.TLD_CONFIG_NOT_FOUND);
        }

        // The module is stored as the plugin declares its id, whatever case the caller typed.
        var module = _modules.FindId(cmd.RegistrarModule);
        if (module is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.TLD_REGISTRAR_MODULE_UNKNOWN);
        }

        entity.Update(
            module,
            cmd.Currency,
            cmd.SellCurrency,
            cmd.IsEnabled,
            cmd.SortOrder,
            cmd.Categories);

        entity.UpdateCostPrices(cmd.CostRegister, cmd.CostTransfer, cmd.CostRenew);
        entity.UpdateSellPrices(cmd.SellRegister, cmd.SellTransfer, cmd.SellRenew);

        await _context.SaveChangesAsync(ct);

        // Invalidate the TLD pricing cache so clients see fresh prices.
        _cache.Remove("tld-pricing-configs");

        return Result.Success();
    }
}
