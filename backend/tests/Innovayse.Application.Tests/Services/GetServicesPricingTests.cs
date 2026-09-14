namespace Innovayse.Application.Tests.Services;

using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Services.Queries.GetServices;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Domains.Interfaces;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;
using Moq;
using Xunit;

/// <summary>
/// The admin services grid prices each service in its owning client's currency, from the
/// product's per-currency rows, and says which currency that is.
/// </summary>
public sealed class GetServicesPricingTests
{
    /// <summary>The currency the client under test is billed in.</summary>
    private static readonly Currency Amd = Currency.Create("AMD", "051", "", " ֏", 0, 0.0025m, isBase: false);

    /// <summary>A product priced in both USD and AMD, with distinct figures per cycle.</summary>
    private static Product PricedProduct()
    {
        var product = Product.Create(1, "Starter", null, null, null, null, ProductType.SharedHosting, 2.99m, 29.99m);
        product.SetPrice("USD", BillingCycle.Monthly, 2.99m);
        product.SetPrice("USD", BillingCycle.Annual, 29.99m);
        product.SetPrice("AMD", BillingCycle.Monthly, 1200m);
        product.SetPrice("AMD", BillingCycle.Annual, 12000m);
        return product;
    }

    /// <summary>Builds the handler over one AMD client owning the given services.</summary>
    private static GetServicesHandler Handler(Product product, params ClientService[] services)
    {
        var serviceRepo = new Mock<IClientServiceRepository>();
        serviceRepo.Setup(s => s.ListAsync(1, 20, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<ClientService>)services, services.Length));
        var products = new Mock<IProductRepository>();
        products.Setup(p => p.FindByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([product]);
        var clients = new Mock<IClientRepository>();
        clients.Setup(c => c.FindByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Client.Create("u-5", "Ara", "Petrosyan", "ara@example.test")]);
        var domains = new Mock<IDomainRepository>();
        domains.Setup(d => d.FindDomainNamesByServiceIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var resolver = new Mock<IPayerCurrencyResolver>();
        resolver.Setup(r => r.ForClientAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(Amd);
        return new GetServicesHandler(serviceRepo.Object, products.Object, clients.Object, domains.Object, resolver.Object);
    }

    [Fact]
    public async Task Handle_PricesEachServiceInTheClientsCurrencyForItsCycle()
    {
        var product = PricedProduct();
        var monthly = ClientService.Create(5, product.Id, "monthly");
        var annual = ClientService.Create(5, product.Id, "annual");

        var page = await Handler(product, monthly, annual).HandleAsync(new GetServicesQuery(), CancellationToken.None);

        Assert.Collection(page.Items,
            m =>
            {
                Assert.Equal(1200m, m.Price);
                Assert.Equal("AMD", m.PriceCurrency);
            },
            a =>
            {
                Assert.Equal(12000m, a.Price);
                Assert.Equal("AMD", a.PriceCurrency);
            });
    }

    [Fact]
    public async Task Handle_ServiceWithoutAPriceInTheClientsCurrencyShowsZeroNotTheBaseFigure()
    {
        var product = PricedProduct();
        product.RemovePrices("AMD");
        var service = ClientService.Create(5, product.Id, "monthly");

        var page = await Handler(product, service).HandleAsync(new GetServicesQuery(), CancellationToken.None);

        var item = Assert.Single(page.Items);
        Assert.Equal(0m, item.Price);
        Assert.Equal("AMD", item.PriceCurrency);
    }
}
