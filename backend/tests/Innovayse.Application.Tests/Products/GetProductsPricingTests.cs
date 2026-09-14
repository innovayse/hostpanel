namespace Innovayse.Application.Tests.Products;

using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Products.Queries.GetProducts;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Moq;
using Xunit;

/// <summary>The product list is priced in the caller's currency and hides what they cannot buy.</summary>
public sealed class GetProductsPricingTests
{
    /// <summary>The caller's currency in every test here.</summary>
    private static readonly Currency Amd = Currency.Create("AMD", "051", "", " ֏", 0, 0.0025m, isBase: false);

    /// <summary>Two products: one sold in AMD and USD, one sold in USD only.</summary>
    private static (Product Both, Product UsdOnly) Products()
    {
        var both = Product.Create(1, "Starter", null, null, null, null, ProductType.SharedHosting, 0m, 0m);
        both.SetPrice("USD", BillingCycle.Monthly, 2.99m);
        both.SetPrice("AMD", BillingCycle.Monthly, 1200m);
        var usdOnly = Product.Create(1, "Pro", null, null, null, null, ProductType.SharedHosting, 0m, 0m);
        usdOnly.SetPrice("USD", BillingCycle.Monthly, 9.99m);
        usdOnly.SetPrice("USD", BillingCycle.Annual, 99.99m);
        return (both, usdOnly);
    }

    /// <summary>Builds the handler over the two products with an AMD caller.</summary>
    private static GetProductsHandler Handler(Product both, Product usdOnly)
    {
        var repo = new Mock<IProductRepository>();
        repo.Setup(r => r.ListAsync(null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([both, usdOnly]);
        var resolver = new Mock<IPayerCurrencyResolver>();
        resolver.Setup(r => r.ForCallerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Amd);
        return new GetProductsHandler(repo.Object, resolver.Object);
    }

    [Fact]
    public async Task Handle_StorefrontOmitsProductsWithNoPriceInTheCallersCurrency()
    {
        var (both, usdOnly) = Products();

        var result = await Handler(both, usdOnly).HandleAsync(new GetProductsQuery(), CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal("Starter", dto.Name);
        Assert.Equal(1200m, dto.Pricing.Monthly);
        Assert.Null(dto.Pricing.Annual);
        Assert.Equal(2, dto.Prices.Count);
        Assert.Contains(dto.Prices, p => p.CurrencyCode == "USD" && p.Monthly == 2.99m && p.Annual is null);
    }

    [Fact]
    public async Task Handle_AdminListIncludesUnsellableProductsWithNullPricing()
    {
        var (both, usdOnly) = Products();

        var result = await Handler(both, usdOnly)
            .HandleAsync(new GetProductsQuery(IncludeUnsellable: true), CancellationToken.None);

        Assert.Equal(2, result.Count);
        var pro = Assert.Single(result, p => p.Name == "Pro");
        Assert.Null(pro.Pricing.Monthly);
        Assert.Null(pro.Pricing.Annual);
        var usd = Assert.Single(pro.Prices);
        Assert.Equal(("USD", 9.99m, 99.99m), (usd.CurrencyCode, usd.Monthly, usd.Annual));
    }
}
