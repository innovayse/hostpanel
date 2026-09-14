namespace Innovayse.Application.Tests.Services;

using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Resources;
using Innovayse.Application.Services.Commands.OrderService;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

/// <summary>
/// A service's amounts come from the order line when given — zero included — and from the
/// product's price in the client's currency only when the caller left them null.
/// </summary>
public sealed class OrderServicePricingTests
{
    /// <summary>The client's currency.</summary>
    private static readonly Currency Amd = Currency.Create("AMD", "051", "", " ֏", 0, 0.0025m, isBase: false);

    /// <summary>A product sold in USD only.</summary>
    private static Product UsdOnlyProduct()
    {
        var product = Product.Create(1, "Starter", null, null, null, null, ProductType.SharedHosting, 0m, 0m);
        product.SetPrice("USD", BillingCycle.Monthly, 2.99m);
        return product;
    }

    /// <summary>Builds the handler over the product for an AMD client, capturing the added service.</summary>
    private static (OrderServiceHandler Handler, Func<ClientService?> Added) Handler(Product product)
    {
        ClientService? added = null;
        var services = new Mock<IClientServiceRepository>();
        services.Setup(s => s.Add(It.IsAny<ClientService>())).Callback<ClientService>(s => added = s);
        var products = new Mock<IProductRepository>();
        products.Setup(p => p.FindByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var localizer = new Mock<IStringLocalizer<ValidationMessages>>();
        localizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns(new LocalizedString("ProductNotAvailable", "Refused."));
        var resolver = new Mock<IPayerCurrencyResolver>();
        resolver.Setup(r => r.ForClientAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(Amd);
        var handler = new OrderServiceHandler(
            services.Object, products.Object, new Mock<IUnitOfWork>().Object, localizer.Object, resolver.Object);
        return (handler, () => added);
    }

    [Fact]
    public async Task Handle_ZeroAmountsFromFulfilmentAreHonouredNotRefused()
    {
        var product = UsdOnlyProduct();
        var (handler, added) = Handler(product);

        await handler.HandleAsync(
            new OrderServiceCommand(5, product.Id, "monthly", 0m, 0m, "Stripe"), CancellationToken.None);

        var service = added();
        Assert.NotNull(service);
        Assert.Equal(0m, service!.FirstPaymentAmount);
        Assert.Equal(0m, service.RecurringAmount);
    }

    [Fact]
    public async Task Handle_NullAmountsAreRefusedWhenTheProductHasNoPriceInTheClientsCurrency()
    {
        var product = UsdOnlyProduct();
        var (handler, _) = Handler(product);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(
            new OrderServiceCommand(5, product.Id, "monthly", null, null), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NullAmountsReadTheProductsPriceInTheClientsCurrency()
    {
        var product = UsdOnlyProduct();
        product.SetPrice("AMD", BillingCycle.Monthly, 1200m);
        var (handler, added) = Handler(product);

        await handler.HandleAsync(
            new OrderServiceCommand(5, product.Id, "monthly", null, null), CancellationToken.None);

        var service = added();
        Assert.NotNull(service);
        Assert.Equal(1200m, service!.FirstPaymentAmount);
        Assert.Equal(1200m, service.RecurringAmount);
    }
}
