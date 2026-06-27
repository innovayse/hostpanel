namespace Innovayse.Application.Domains.Commands.ImportTldPricing;

using Innovayse.Application.Common;
using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles <see cref="ImportTldPricingCommand"/> by fetching TLD pricing from the specified
/// registrar provider and upserting TLD configurations: updating cost prices for existing TLDs
/// and creating new configurations for unknown TLDs.
/// Invalidates the TLD pricing cache after import to ensure clients see fresh data.
/// </summary>
/// <param name="repo">TLD configuration repository for lookups and persistence.</param>
/// <param name="factory">Factory for resolving registrar provider implementations by module.</param>
/// <param name="uow">Unit of work for committing all changes in a single transaction.</param>
/// <param name="logger">Logger for recording import progress and diagnostics.</param>
/// <param name="cache">Memory cache for invalidating pricing data.</param>
public sealed class ImportTldPricingHandler(
    ITldConfigRepository repo,
    IRegistrarProviderFactory factory,
    IUnitOfWork uow,
    ILogger<ImportTldPricingHandler> logger,
    IMemoryCache cache)
{
    /// <summary>
    /// Imports TLD pricing from the specified registrar module, creating new configurations
    /// for unknown TLDs and updating cost prices for existing ones.
    /// </summary>
    /// <param name="cmd">The import command specifying which registrar module to import from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="ImportTldPricingResult"/> with counts of imported and updated TLDs.</returns>
    public async Task<ImportTldPricingResult> HandleAsync(ImportTldPricingCommand cmd, CancellationToken ct)
    {
        var module = Enum.Parse<RegistrarModule>(cmd.Module);
        var provider = factory.GetProvider(module);

        var pricingList = await provider.GetTldPricingAsync(ct);

        logger.LogInformation(
            "ImportTldPricing: received {Count} TLD(s) from {Module}.", pricingList.Count, module);

        var imported = 0;
        var updated = 0;

        foreach (var pricing in pricingList)
        {
            var tld = pricing.Tld.ToLower().Trim();
            var existing = await repo.FindByTldAsync(tld, ct);

            // Convert AMD prices to USD for sell prices (AMD / 390 = USD)
            var sellRegister = ConvertAmdToUsd(pricing.Register);
            var sellTransfer = ConvertAmdToUsd(pricing.Transfer);
            var sellRenew = ConvertAmdToUsd(pricing.Renew);

            if (existing is not null)
            {
                existing.Update(existing.RegistrarModule, pricing.Currency, existing.SellCurrency, true, existing.SortOrder, existing.Categories.ToList());
                existing.UpdateCostPrices(pricing.Register, pricing.Transfer, pricing.Renew);
                existing.UpdateSellPrices(sellRegister, sellTransfer, sellRenew);
                existing.MarkSynced();
                updated++;
            }
            else
            {
                var entity = TldConfig.Create(
                    tld,
                    module,
                    pricing.Currency,
                    "USD",
                    isEnabled: true,
                    sortOrder: 0,
                    categories: []);

                entity.UpdateCostPrices(pricing.Register, pricing.Transfer, pricing.Renew);
                entity.UpdateSellPrices(sellRegister, sellTransfer, sellRenew);

                repo.Add(entity);
                imported++;
            }
        }

        await uow.SaveChangesAsync(ct);

        // Invalidate the TLD pricing cache so clients see fresh pricing data
        cache.Remove("tld-pricing-configs");

        logger.LogInformation(
            "ImportTldPricing: completed for {Module} — {Imported} imported, {Updated} updated.",
            module, imported, updated);

        return new ImportTldPricingResult(imported, updated);
    }

    /// <summary>
    /// Converts AMD prices to USD using the fixed exchange rate: 1 USD = 390 AMD.
    /// </summary>
    /// <param name="amdPrices">Dictionary of period (years) → price in AMD.</param>
    /// <returns>Dictionary of period (years) → price in USD.</returns>
    private static Dictionary<int, decimal> ConvertAmdToUsd(IReadOnlyDictionary<int, decimal> amdPrices)
    {
        const decimal AmdPerUsd = 390m;
        var result = new Dictionary<int, decimal>();
        foreach (var (period, amdPrice) in amdPrices)
        {
            result[period] = Math.Round(amdPrice / AmdPerUsd, 2);
        }
        return result;
    }
}
