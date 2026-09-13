namespace Innovayse.Application.Tests.Products;

using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Products.Commands.UpdateProduct;
using Innovayse.Application.Products.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Moq;
using Xunit;

/// <summary>Updating a product writes its per-currency prices and removes the ones left out.</summary>
public sealed class UpdateProductPricesTests
{
    /// <summary>The base currency the legacy pair is read as.</summary>
    private static readonly Currency Usd = Currency.Create("USD", "840", "$", "", 2, 1m, isBase: true);

    /// <summary>A resolver answering USD as the base.</summary>
    private static IPayerCurrencyResolver Resolver()
    {
        var resolver = new Mock<IPayerCurrencyResolver>();
        resolver.Setup(r => r.BaseAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Usd);
        return resolver.Object;
    }

    /// <summary>Prices given are set; a currency absent from the list is removed.</summary>
    [Fact]
    public async Task Handle_WritesGivenPricesAndDropsTheRest()
    {
        var product = Product.Create(1, "Starter", null, null, null, null, ProductType.SharedHosting, 0m, 0m);
        product.SetPrice("EUR", BillingCycle.Monthly, 2.79m);
        var repo = new Mock<IProductRepository>();
        repo.Setup(r => r.FindByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var uow = new Mock<IUnitOfWork>();

        var handler = new UpdateProductHandler(repo.Object, uow.Object, Resolver());
        await handler.HandleAsync(new UpdateProductCommand(7, "Starter", null, null, null, null,
            [new ProductPriceInput("USD", 2.99m, 29.99m), new ProductPriceInput("AMD", 1200m, null)], null), CancellationToken.None);

        Assert.Equal(2.99m, product.PriceFor("USD", BillingCycle.Monthly));
        Assert.Equal(29.99m, product.PriceFor("USD", BillingCycle.Annual));
        Assert.Equal(1200m, product.PriceFor("AMD", BillingCycle.Monthly));
        Assert.Null(product.PriceFor("AMD", BillingCycle.Annual));
        Assert.False(product.SellsIn("EUR"));
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A cycle the admin leaves null within a kept currency is removed, not left at its old amount.</summary>
    [Fact]
    public async Task Handle_DropsACycleLeftNullWithinAKeptCurrency()
    {
        var product = Product.Create(1, "Starter", null, null, null, null, ProductType.SharedHosting, 0m, 0m);
        product.SetPrice("USD", BillingCycle.Monthly, 2.99m);
        product.SetPrice("USD", BillingCycle.Annual, 29.99m);
        var repo = new Mock<IProductRepository>();
        repo.Setup(r => r.FindByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var uow = new Mock<IUnitOfWork>();

        var handler = new UpdateProductHandler(repo.Object, uow.Object, Resolver());
        await handler.HandleAsync(new UpdateProductCommand(7, "Starter", null, null, null, null,
            [new ProductPriceInput("usd", 3.49m, null)], null), CancellationToken.None);

        Assert.Equal(3.49m, product.PriceFor("USD", BillingCycle.Monthly));
        Assert.Null(product.PriceFor("USD", BillingCycle.Annual));
    }

    /// <summary>An empty list clears every price; the product stays but sells nowhere.</summary>
    [Fact]
    public async Task Handle_EmptyListMakesTheProductUnsellable()
    {
        var product = Product.Create(1, "Starter", null, null, null, null, ProductType.SharedHosting, 0m, 0m);
        product.SetPrice("USD", BillingCycle.Monthly, 2.99m);
        var repo = new Mock<IProductRepository>();
        repo.Setup(r => r.FindByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var uow = new Mock<IUnitOfWork>();

        var handler = new UpdateProductHandler(repo.Object, uow.Object, Resolver());
        await handler.HandleAsync(new UpdateProductCommand(7, "Starter", null, null, null, null, [], null), CancellationToken.None);

        Assert.Empty(product.Prices);
        Assert.False(product.SellsIn("USD"));
    }

    /// <summary>The pre-multi-currency admin form sends the legacy pair and no list; it prices the base currency.</summary>
    [Fact]
    public async Task Handle_LegacyPairWithoutAListPricesTheBaseCurrency()
    {
        var product = Product.Create(1, "Starter", null, null, null, null, ProductType.SharedHosting, 0m, 0m);
        product.SetPrice("AMD", BillingCycle.Monthly, 1200m);
        var repo = new Mock<IProductRepository>();
        repo.Setup(r => r.FindByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var uow = new Mock<IUnitOfWork>();

        var handler = new UpdateProductHandler(repo.Object, uow.Object, Resolver());
        await handler.HandleAsync(new UpdateProductCommand(7, "Starter", null, null, null, null, null, null,
            MonthlyPrice: 4.99m, AnnualPrice: 49.99m), CancellationToken.None);

        Assert.Equal(4.99m, product.PriceFor("USD", BillingCycle.Monthly));
        Assert.Equal(49.99m, product.PriceFor("USD", BillingCycle.Annual));
        Assert.False(product.SellsIn("AMD"));
    }
}
