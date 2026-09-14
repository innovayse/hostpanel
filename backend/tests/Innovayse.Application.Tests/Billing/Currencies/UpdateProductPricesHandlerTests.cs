namespace Innovayse.Application.Tests.Billing.Currencies;

using Innovayse.Application.Billing.Currencies.Commands.UpdateProductPrices;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Moq;
using Xunit;

/// <summary>"Update product prices" writes converted figures into the non-base rows and rounds per currency.</summary>
public sealed class UpdateProductPricesHandlerTests
{
    /// <summary>The base currency, whose prices are the source.</summary>
    private readonly Currency usd = Currency.Create("USD", "840", "$", string.Empty, 2, 1m, isBase: true);

    /// <summary>A zero-decimal currency at the rate the storefront carried: 1 AMD = 0.0025641 USD.</summary>
    private readonly Currency amd = Currency.Create("AMD", "051", string.Empty, " ֏", 0, 0.0025641m, isBase: false);

    /// <summary>The catalogue the handler walks.</summary>
    private readonly List<Product> products = [];

    /// <summary>Records whether the handler saved.</summary>
    private readonly Mock<IUnitOfWork> uow = new();

    /// <summary>Builds a product with the given base-currency prices.</summary>
    /// <param name="monthly">The USD monthly price, or null for none.</param>
    /// <param name="annual">The USD annual price, or null for none.</param>
    /// <returns>The product, already in <see cref="products"/>.</returns>
    private Product ProductPricedInUsd(decimal? monthly, decimal? annual)
    {
        var product = Product.Create(1, "Starter", null, null, null, null, ProductType.SharedHosting, 0m, 0m);
        if (monthly is not null)
        {
            product.SetPrice("USD", BillingCycle.Monthly, monthly.Value);
        }

        if (annual is not null)
        {
            product.SetPrice("USD", BillingCycle.Annual, annual.Value);
        }

        products.Add(product);
        return product;
    }

    /// <summary>Wires the handler over USD (base) and AMD and the products added so far.</summary>
    /// <returns>The handler under test.</returns>
    private UpdateProductPricesHandler Handler()
    {
        var currencies = new Mock<ICurrencyRepository>();
        currencies.Setup(c => c.GetBaseAsync(It.IsAny<CancellationToken>())).ReturnsAsync(usd);
        currencies.Setup(c => c.FindAsync("USD", It.IsAny<CancellationToken>())).ReturnsAsync(usd);
        currencies.Setup(c => c.FindAsync("AMD", It.IsAny<CancellationToken>())).ReturnsAsync(amd);
        currencies.Setup(c => c.FindAsync("XXX", It.IsAny<CancellationToken>())).ReturnsAsync((Currency?)null);

        var productRepo = new Mock<IProductRepository>();
        productRepo.Setup(r => r.ListAsync(null, false, It.IsAny<CancellationToken>())).ReturnsAsync(products);

        return new UpdateProductPricesHandler(currencies.Object, productRepo.Object, uow.Object);
    }

    /// <summary>$2.99 at 1 AMD = 0.0025641 USD → 1166.1 AMD → rounded to 1166 (0 decimals).</summary>
    [Fact]
    public async Task Handle_ConvertsFromBaseAndRoundsToTheCurrencysDecimals()
    {
        var product = ProductPricedInUsd(monthly: 2.99m, annual: null);

        var written = await Handler().HandleAsync(new UpdateProductPricesCommand(["AMD"]), CancellationToken.None);

        Assert.Equal(1, written);
        Assert.Equal(1166m, product.PriceFor("AMD", BillingCycle.Monthly));
        Assert.Equal(2.99m, product.PriceFor("USD", BillingCycle.Monthly));
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>The base currency's own prices are never touched — they are the source.</summary>
    [Fact]
    public async Task Handle_RefusesToTargetTheBase()
    {
        ProductPricedInUsd(monthly: 2.99m, annual: 29.99m);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Handler().HandleAsync(new UpdateProductPricesCommand(["USD"]), CancellationToken.None));

        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A code that is not a configured currency is refused by name.</summary>
    [Fact]
    public async Task Handle_RefusesAnUnconfiguredCurrency()
    {
        ProductPricedInUsd(monthly: 2.99m, annual: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Handler().HandleAsync(new UpdateProductPricesCommand(["XXX"]), CancellationToken.None));

        Assert.Contains("XXX", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>A product with no base price for a cycle gets no converted price for it either.</summary>
    [Fact]
    public async Task Handle_SkipsCyclesTheBaseDoesNotPrice()
    {
        var product = ProductPricedInUsd(monthly: null, annual: 29.99m);

        var written = await Handler().HandleAsync(new UpdateProductPricesCommand(["AMD"]), CancellationToken.None);

        Assert.Equal(1, written);
        Assert.Null(product.PriceFor("AMD", BillingCycle.Monthly));
        Assert.Equal(11696m, product.PriceFor("AMD", BillingCycle.Annual));
    }

    /// <summary>An existing target row is overwritten, not duplicated, and the count is per row written.</summary>
    [Fact]
    public async Task Handle_OverwritesAnExistingTargetRow()
    {
        var product = ProductPricedInUsd(monthly: 2.99m, annual: 29.99m);
        product.SetPrice("AMD", BillingCycle.Monthly, 999m);

        var written = await Handler().HandleAsync(new UpdateProductPricesCommand(["AMD"]), CancellationToken.None);

        Assert.Equal(2, written);
        Assert.Equal(1166m, product.PriceFor("AMD", BillingCycle.Monthly));
        Assert.Single(product.Prices, p => p.CurrencyCode == "AMD" && p.Cycle == BillingCycle.Monthly);
    }
}
