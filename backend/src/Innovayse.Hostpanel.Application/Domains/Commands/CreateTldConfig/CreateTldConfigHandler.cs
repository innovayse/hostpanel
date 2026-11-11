using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Innovayse.Hostpanel.Application.Domains.Commands.CreateTldConfig;

/// <summary>
/// Handles <see cref="CreateTldConfigCommand"/> by validating uniqueness,
/// creating a new <see cref="TldConfig"/> aggregate with prices, and persisting it.
/// Invalidates the TLD pricing cache after creation.
/// </summary>
public sealed class CreateTldConfigHandler
{
    /// <summary>The data context the uniqueness check runs against and the new configuration is persisted through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Memory cache for invalidating pricing data.</summary>
    private readonly IMemoryCache _cache;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context the uniqueness check runs against and the new configuration is persisted through.</param>
    /// <param name="cache">Memory cache for invalidating pricing data.</param>
    public CreateTldConfigHandler(IHostpanelDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    /// <summary>
    /// Creates and persists a new TLD configuration with cost and sell pricing.
    /// </summary>
    /// <param name="cmd">The create TLD configuration command with all settings and pricing data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with the newly assigned TLD configuration identifier, or
    /// <c>TLD_REGISTRAR_MODULE_UNKNOWN</c> (400) when the module names no known registrar, or
    /// <c>TLD_CONFIG_ALREADY_EXISTS</c> (409) when the extension is already configured.
    /// </returns>
    public async Task<Result<int>> HandleAsync(CreateTldConfigCommand cmd, CancellationToken ct)
    {
        // TryParse, not Parse: Parse raises ArgumentException, which no handler in the pipeline
        // classifies, so a misspelled module would come back as an unhandled 500 rather than a
        // refusal the caller can act on.
        if (!Enum.TryParse<RegistrarModule>(cmd.RegistrarModule, ignoreCase: true, out var module))
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.TLD_REGISTRAR_MODULE_UNKNOWN);
        }

        var normalizedTld = cmd.Tld.ToLower().Trim();

        var existing = await _context.TldConfigs.FirstOrDefaultAsync(x => x.Tld == normalizedTld, ct);
        if (existing is not null)
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.TLD_CONFIG_ALREADY_EXISTS);
        }

        var entity = TldConfig.Create(
            normalizedTld,
            module,
            cmd.Currency,
            cmd.SellCurrency,
            cmd.IsEnabled,
            cmd.SortOrder,
            cmd.Categories);

        entity.UpdateCostPrices(cmd.CostRegister, cmd.CostTransfer, cmd.CostRenew);
        entity.UpdateSellPrices(cmd.SellRegister, cmd.SellTransfer, cmd.SellRenew);

        _context.TldConfigs.Add(entity);
        await _context.SaveChangesAsync(ct);

        // Invalidate the TLD pricing cache so clients see the new TLD.
        _cache.Remove("tld-pricing-configs");

        return Result.Success(entity.Id);
    }
}
