namespace Innovayse.Application.Billing.Currencies.Commands.UpdateProductPrices;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;

/// <summary>Handles <see cref="UpdateProductPricesCommand"/>.</summary>
/// <param name="currencies">The configured currencies.</param>
/// <param name="productRepo">The catalogue, prices included.</param>
/// <param name="uow">Unit of work for persistence.</param>
public sealed class UpdateProductPricesHandler(
    ICurrencyRepository currencies,
    IProductRepository productRepo,
    IUnitOfWork uow)
{
    /// <summary>Converts every product's base prices into each target currency and stores them.</summary>
    /// <param name="cmd">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>How many price rows were written.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a code is not a configured currency, or names the base.
    /// </exception>
    public async Task<int> HandleAsync(UpdateProductPricesCommand cmd, CancellationToken ct)
    {
        var baseCurrency = await currencies.GetBaseAsync(ct);
        var targets = await ResolveTargetsAsync(cmd.Currencies, ct);

        var products = await productRepo.ListAsync(groupId: null, activeOnly: false, ct);
        var written = 0;

        foreach (var product in products)
        {
            foreach (var cycle in Enum.GetValues<BillingCycle>())
            {
                var basePrice = product.PriceFor(baseCurrency.Code, cycle);
                if (basePrice is null)
                {
                    // Nothing to convert from: a cycle the base does not price is not for sale
                    // in any currency, and inventing a figure would put it on sale.
                    continue;
                }

                foreach (var target in targets)
                {
                    // The base price is in base units; one target unit is RateToBase base units.
                    var converted = target.Round(basePrice.Value / target.RateToBase);
                    product.SetPrice(target.Code, cycle, converted);
                    written++;
                }
            }
        }

        await uow.SaveChangesAsync(ct);
        return written;
    }

    /// <summary>Looks up each requested code and refuses the base or anything unconfigured.</summary>
    /// <param name="codes">The codes asked for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The target currencies, in the order asked.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a code is not a configured currency, or names the base.
    /// </exception>
    private async Task<List<Currency>> ResolveTargetsAsync(IReadOnlyList<string> codes, CancellationToken ct)
    {
        List<Currency> targets = [];

        foreach (var code in codes)
        {
            var currency = await currencies.FindAsync(code, ct)
                ?? throw new InvalidOperationException($"'{code}' is not a configured currency.");

            if (currency.IsBase)
            {
                throw new InvalidOperationException(
                    "The base currency's prices are the source; they are not recomputed.");
            }

            targets.Add(currency);
        }

        return targets;
    }
}
