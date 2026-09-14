namespace Innovayse.Application.Tests.Products;

using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Products.Queries.GetProducts;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Moq;
using Xunit;

/// <summary>The product list is priced in the caller's currency and hides what they cannot buy.</summary>
public sealed class GetProductsPricingTests
{
    /// <summary>The caller's currency in every test here.</summary>
    private static readonly Currency Amd = Currency.Create("AMD", "051", "", " ֏", 0, 0.0025m, isBase: false);

    /// <summary>The base/resolver fallback currency for tests exercising the anonymous "currency" parameter.</summary>
    private static readonly Currency Usd = Currency.Create("USD", "840", "$", "", 2, 1m, isBase: true);

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
    private static GetProductsHandler Handler(Product both, Product usdOnly) =>
        Handler(both, usdOnly, userId: null, requestedCurrency: null);

    /// <summary>
    /// Builds the handler over the two products, letting a test choose whether the caller is
    /// signed in (<paramref name="userId"/> non-null) and what code the query requests.
    /// The resolver always answers AMD (the client's recorded currency, or the base for a guest
    /// with no accepted request); <paramref name="requestedCurrency"/> is only consulted when
    /// <paramref name="userId"/> is <see langword="null"/>, per <see cref="GetProductsHandler"/>.
    /// </summary>
    private static GetProductsHandler Handler(
        Product both, Product usdOnly, string? userId, string? requestedCurrency)
    {
        var repo = new Mock<IProductRepository>();
        repo.Setup(r => r.ListAsync(null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([both, usdOnly]);

        var resolver = new Mock<IPayerCurrencyResolver>();
        resolver.Setup(r => r.ForCallerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Amd);

        var currencies = new Mock<ICurrencyRepository>();
        currencies.Setup(c => c.FindAsync("AMD", It.IsAny<CancellationToken>())).ReturnsAsync(Amd);
        currencies.Setup(c => c.FindAsync("USD", It.IsAny<CancellationToken>())).ReturnsAsync(Usd);
        currencies.Setup(c => c.FindAsync(
                It.Is<string>(code => code != "AMD" && code != "USD"), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Currency?)null);

        var caller = new Mock<ICurrentRequestContext>();
        caller.SetupGet(c => c.UserId).Returns(userId);

        return new GetProductsHandler(repo.Object, resolver.Object, currencies.Object, caller.Object);
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

    [Fact]
    public async Task Handle_AnonymousCallerWithEnabledCurrencyIsPricedInIt()
    {
        var (both, usdOnly) = Products();
        var handler = Handler(both, usdOnly, userId: null, requestedCurrency: "AMD");

        var result = await handler.HandleAsync(
            new GetProductsQuery(Currency: "AMD"), CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal("Starter", dto.Name);
        Assert.Equal(1200m, dto.Pricing.Monthly);
    }

    [Fact]
    public async Task Handle_SignedInClientIgnoresTheCurrencyParameter()
    {
        // The client's own recorded currency (AMD, via the resolver mock) always wins, so asking
        // for "USD" here must not change which products come back or how they are priced.
        var (both, usdOnly) = Products();
        var handler = Handler(both, usdOnly, userId: "client-1", requestedCurrency: "USD");

        var result = await handler.HandleAsync(
            new GetProductsQuery(Currency: "USD"), CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal("Starter", dto.Name);
        Assert.Equal(1200m, dto.Pricing.Monthly);
    }

    [Fact]
    public async Task Handle_AnonymousCallerWithUnknownCurrencyFallsBackToTheResolver()
    {
        var (both, usdOnly) = Products();
        var handler = Handler(both, usdOnly, userId: null, requestedCurrency: "ZZZ");

        var result = await handler.HandleAsync(
            new GetProductsQuery(Currency: "ZZZ"), CancellationToken.None);

        // The resolver mock still answers AMD (the guest's resolved base in this test setup),
        // so an unrecognised code behaves exactly like no code at all.
        var dto = Assert.Single(result);
        Assert.Equal(1200m, dto.Pricing.Monthly);
    }
}
