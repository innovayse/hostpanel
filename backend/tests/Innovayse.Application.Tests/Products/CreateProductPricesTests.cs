namespace Innovayse.Application.Tests.Products;

using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Products.Commands.CreateProduct;
using Innovayse.Application.Products.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Moq;
using Xunit;

/// <summary>Creating a product writes its per-currency prices, from the list or from the legacy pair.</summary>
public sealed class CreateProductPricesTests
{
    /// <summary>The base currency the legacy pair is read as.</summary>
    private static readonly Currency Usd = Currency.Create("USD", "840", "$", "", 2, 1m, isBase: true);

    /// <summary>Builds the handler over mocks that capture the added product.</summary>
    private static (CreateProductHandler Handler, Mock<IProductRepository> Repo) Handler()
    {
        var repo = new Mock<IProductRepository>();
        var groups = new Mock<IProductGroupRepository>();
        groups.Setup(g => g.FindByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProductGroup.Create("Hosting", null));
        var resolver = new Mock<IPayerCurrencyResolver>();
        resolver.Setup(r => r.BaseAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Usd);
        return (new CreateProductHandler(repo.Object, groups.Object, new Mock<IUnitOfWork>().Object, resolver.Object), repo);
    }

    [Fact]
    public async Task Handle_LegacyPairOnlyYieldsBaseCurrencyRows()
    {
        var (handler, repo) = Handler();
        Product? added = null;
        repo.Setup(r => r.Add(It.IsAny<Product>())).Callback<Product>(p => added = p);

        await handler.HandleAsync(new CreateProductCommand(1, "Starter", null, null, null, null,
            ProductType.SharedHosting, null, null, MonthlyPrice: 4.99m, AnnualPrice: 49.99m), CancellationToken.None);

        Assert.NotNull(added);
        Assert.Equal(4.99m, added!.PriceFor("USD", BillingCycle.Monthly));
        Assert.Equal(49.99m, added.PriceFor("USD", BillingCycle.Annual));
        Assert.Single(added.Prices.Select(p => p.CurrencyCode).Distinct());
        Assert.Equal(4.99m, added.MonthlyPrice);
        Assert.Equal(49.99m, added.AnnualPrice);
    }

    [Fact]
    public async Task Handle_ListWinsOverTheLegacyPair()
    {
        var (handler, repo) = Handler();
        Product? added = null;
        repo.Setup(r => r.Add(It.IsAny<Product>())).Callback<Product>(p => added = p);

        await handler.HandleAsync(new CreateProductCommand(1, "Starter", null, null, null, null,
            ProductType.SharedHosting, [new ProductPriceInput("AMD", 1200m, null)], null,
            MonthlyPrice: 4.99m, AnnualPrice: 49.99m), CancellationToken.None);

        Assert.NotNull(added);
        Assert.Equal(1200m, added!.PriceFor("AMD", BillingCycle.Monthly));
        Assert.False(added.SellsIn("USD"));
        Assert.Equal(0m, added.MonthlyPrice);
        Assert.Equal(0m, added.AnnualPrice);
    }
}
