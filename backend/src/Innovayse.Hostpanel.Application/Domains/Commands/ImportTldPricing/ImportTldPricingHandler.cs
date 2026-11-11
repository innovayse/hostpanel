using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Domains.Commands.ImportTldPricing;

/// <summary>
/// Handles <see cref="ImportTldPricingCommand"/> by fetching TLD pricing from the specified
/// registrar provider and upserting TLD configurations: updating cost prices for existing TLDs
/// and creating new configurations for unknown TLDs.
/// Invalidates the TLD pricing cache after import to ensure clients see fresh data.
/// </summary>
public sealed class ImportTldPricingHandler
{
    /// <summary>The data context TLD configurations are looked up in and persisted through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Factory for resolving registrar provider implementations by module.</summary>
    private readonly IRegistrarProviderFactory _factory;

    /// <summary>Logger for recording import progress and diagnostics.</summary>
    private readonly ILogger<ImportTldPricingHandler> _logger;

    /// <summary>Memory cache for invalidating pricing data.</summary>
    private readonly IMemoryCache _cache;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context TLD configurations are looked up in and persisted through.</param>
    /// <param name="factory">Factory for resolving registrar provider implementations by module.</param>
    /// <param name="logger">Logger for recording import progress and diagnostics.</param>
    /// <param name="cache">Memory cache for invalidating pricing data.</param>
    /// <param name="clock">The source of the current moment.</param>
    public ImportTldPricingHandler(
        IHostpanelDbContext context,
        IRegistrarProviderFactory factory,
        ILogger<ImportTldPricingHandler> logger,
        IMemoryCache cache,
        IClock clock)
    {
        _context = context;
        _factory = factory;
        _logger = logger;
        _cache = cache;
        _clock = clock;
    }

    /// <summary>
    /// Imports TLD pricing from the specified registrar module, creating new configurations
    /// for unknown TLDs and updating cost prices for existing ones.
    /// </summary>
    /// <param name="cmd">The import command specifying which registrar module to import from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with an <see cref="ImportTldPricingResult"/> giving counts of imported and updated
    /// TLDs, or <c>TLD_REGISTRAR_MODULE_UNKNOWN</c> (400) when the module names no known registrar.
    /// </returns>
    public async Task<Result<ImportTldPricingResult>> HandleAsync(ImportTldPricingCommand cmd, CancellationToken ct)
    {
        // TryParse, not Parse: Parse raises ArgumentException, which no handler in the pipeline
        // classifies, so a misspelled module would come back as an unhandled 500 rather than a
        // refusal the caller can act on.
        if (!Enum.TryParse<RegistrarModule>(cmd.Module, ignoreCase: true, out var module))
        {
            return Result<ImportTldPricingResult>.FailureCode(HostpanelErrorCodes.TLD_REGISTRAR_MODULE_UNKNOWN);
        }
        var provider = _factory.GetProvider(module);

        var pricingList = await provider.GetTldPricingAsync(ct);

        _logger.LogInformation(
            "ImportTldPricing: received {Count} TLD(s) from {Module}.", pricingList.Count, module);

        var syncedAt = _clock.UtcNow;
        var imported = 0;
        var updated = 0;

        foreach (var pricing in pricingList)
        {
            var tld = pricing.Tld.ToLower().Trim();
            var existing = await _context.TldConfigs.FirstOrDefaultAsync(x => x.Tld == tld, ct);

            if (existing is not null)
            {
                existing.Update(existing.RegistrarModule, pricing.Currency, pricing.Currency, true, existing.SortOrder, existing.Categories.ToList());
                existing.UpdateCostPrices(pricing.Register, pricing.Transfer, pricing.Renew);
                existing.UpdateSellPrices(pricing.Register, pricing.Transfer, pricing.Renew);
                existing.MarkSynced(syncedAt);
                updated++;
            }
            else
            {
                var entity = TldConfig.Create(
                    tld,
                    module,
                    pricing.Currency,
                    pricing.Currency,
                    isEnabled: true,
                    sortOrder: 0,
                    categories: []);

                entity.UpdateCostPrices(pricing.Register, pricing.Transfer, pricing.Renew);
                entity.UpdateSellPrices(pricing.Register, pricing.Transfer, pricing.Renew);

                _context.TldConfigs.Add(entity);
                imported++;
            }
        }

        await _context.SaveChangesAsync(ct);

        // Invalidate the TLD pricing cache so clients see fresh pricing data.
        _cache.Remove("tld-pricing-configs");

        _logger.LogInformation(
            "ImportTldPricing: completed for {Module} — {Imported} imported, {Updated} updated.",
            module, imported, updated);

        return Result.Success(new ImportTldPricingResult(imported, updated));
    }
}
