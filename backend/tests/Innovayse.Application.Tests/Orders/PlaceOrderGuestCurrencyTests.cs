namespace Innovayse.Application.Tests.Orders;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Billing.Queries.ListAvailablePaymentMethods;
using Innovayse.Application.Common;
using Innovayse.Application.Orders.Commands.PlaceOrder;
using Innovayse.Application.Resources;
using Innovayse.Domain.Auth.Interfaces;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Orders.Interfaces;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Microsoft.Extensions.Localization;
using Moq;
using System.Reflection;
using Wolverine;
using Xunit;

/// <summary>
/// The currency a checkout chooses is the currency the new client is billed in — and only a
/// new client's. A client who already exists already has one.
/// </summary>
public sealed class PlaceOrderGuestCurrencyTests
{
    /// <summary>The product every order here is for.</summary>
    private const int ProductId = 3;

    /// <summary>The subject the provisioner hands back for a guest, and an existing client's subject.</summary>
    private const string Subject = "local-subject-1";

    /// <summary>The clients the handler adds, in order.</summary>
    private readonly List<Client> added = [];

    /// <summary>The client repository; a guest has no row, an existing client has one.</summary>
    private readonly Mock<IClientRepository> clients = new();

    /// <summary>Who is ordering; a guest by default.</summary>
    private readonly Mock<ICurrentRequestContext> caller = new();

    /// <summary>The currencies this panel has configured: USD (base) and AMD enabled, EUR switched off.</summary>
    private readonly Mock<ICurrencyRepository> currencies = new();

    /// <summary>Configures the world every test starts from.</summary>
    public PlaceOrderGuestCurrencyTests()
    {
        clients.Setup(r => r.FindByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Client?)null);
        clients.Setup(r => r.Add(It.IsAny<Client>())).Callback<Client>(added.Add);

        caller.SetupGet(c => c.UserId).Returns((string?)null);

        var usd = Currency.Create("USD", "840", "$", string.Empty, 2, 1m, isBase: true);
        var amd = Currency.Create("AMD", "051", string.Empty, " ֏", 0, 0.0025m, isBase: false);
        var eur = Currency.Create("EUR", "978", "€", string.Empty, 2, 1.1m, isBase: false);
        eur.Disable();

        currencies.Setup(c => c.FindAsync("USD", It.IsAny<CancellationToken>())).ReturnsAsync(usd);
        currencies.Setup(c => c.FindAsync("AMD", It.IsAny<CancellationToken>())).ReturnsAsync(amd);
        currencies.Setup(c => c.FindAsync("EUR", It.IsAny<CancellationToken>())).ReturnsAsync(eur);
        currencies.Setup(c => c.FindAsync("XXX", It.IsAny<CancellationToken>())).ReturnsAsync((Currency?)null);
    }

    /// <summary>Builds the product the order references.</summary>
    /// <returns>An active hosting product carrying <see cref="ProductId"/>.</returns>
    private static Product HostingProduct()
    {
        var product = Product.Create(
            groupId: 1, name: "Pro Hosting", description: null, website: null, slug: null,
            packageName: null, type: ProductType.SharedHosting,
            monthlyPrice: 19.99m, annualPrice: 199.99m);

        // The order reads the per-currency price rows, not the legacy columns; the payer here is
        // billed in the base, so one USD row is what makes the product orderable.
        product.SetPrice("USD", BillingCycle.Monthly, 19.99m);

        // Create leaves Id at 0 for EF to assign, and the handler looks the product up by id.
        typeof(Innovayse.Domain.Common.Entity)
            .GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(product, ProductId);

        return product;
    }

    /// <summary>An order for one month of <see cref="HostingProduct"/> as a guest sends it, with a currency.</summary>
    /// <param name="currency">The currency the checkout chose; null when it chose none.</param>
    /// <returns>The command under test.</returns>
    private static PlaceOrderCommand GuestOrder(string? currency) => new(
        FirstName: "Ada", LastName: "Lovelace", Email: "ada@example.com", Password: "s3cret!", Phone: null,
        PaymentMethod: "stripe",
        Items: [new PlaceOrderItemDto(ProductId, "monthly", null, null)],
        Currency: currency);

    /// <summary>
    /// A bus whose availability query lists exactly the given modules. The handler refuses an
    /// order for any module the checkout is not offering, so every harness has to offer the one
    /// its order names.
    /// </summary>
    private static IMessageBus BusOffering(params string[] modules)
    {
        var bus = new Mock<IMessageBus>();
        bus.Setup(b => b.InvokeAsync<IReadOnlyList<AvailablePaymentMethodDto>>(
                It.IsAny<ListAvailablePaymentMethodsQuery>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(modules.Select(m => new AvailablePaymentMethodDto(m, m)).ToList());
        return bus.Object;
    }

    /// <summary>Builds the handler over the fixture world.</summary>
    /// <returns>The handler under test.</returns>
    private PlaceOrderHandler Handler()
    {
        var products = new Mock<IProductRepository>();
        products.Setup(r => r.FindByIdsAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([HostingProduct()]);

        var orders = new Mock<IOrderRepository>();
        orders.Setup(r => r.GetNextOrderNumberAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var provisioning = new Mock<IUserProvisioning>();
        provisioning.Setup(p => p.CreateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Subject);

        // The base is USD; the invoice's currency is whatever the resolver says, which is not
        // under test here, so it answers the base for everyone.
        var usd = Currency.Create("USD", "840", "$", string.Empty, 2, 1m, isBase: true);
        var payerCurrency = new Mock<IPayerCurrencyResolver>();
        payerCurrency.Setup(r => r.BaseAsync(It.IsAny<CancellationToken>())).ReturnsAsync(usd);
        payerCurrency.Setup(r => r.ForClientAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(usd);

        return new PlaceOrderHandler(
            orders.Object,
            products.Object,
            clients.Object,
            Mock.Of<IInvoiceRepository>(),
            Mock.Of<IUnitOfWork>(),
            provisioning.Object,
            Mock.Of<ISubjectRoleStore>(),
            BusOffering("stripe"),
            caller.Object,
            Mock.Of<IStringLocalizer<ValidationMessages>>(),
            payerCurrency.Object,
            currencies.Object);
    }

    /// <summary>A guest who chose AMD at checkout is billed in AMD from their first client row.</summary>
    [Fact]
    public async Task AGuestOrderWithACurrencyCreatesTheClientInIt()
    {
        await Handler().HandleAsync(GuestOrder("amd"), CancellationToken.None);

        Assert.Single(added);
        Assert.Equal("AMD", added[0].Currency);
    }

    /// <summary>A guest who chose nothing is billed in the base, recorded explicitly rather than left null.</summary>
    [Fact]
    public async Task AGuestOrderWithNoCurrencyGetsTheBase()
    {
        await Handler().HandleAsync(GuestOrder(null), CancellationToken.None);

        Assert.Single(added);
        Assert.Equal("USD", added[0].Currency);
    }

    /// <summary>A currency the panel does not offer is refused before any account exists.</summary>
    /// <param name="currency">An unconfigured code, and a configured one the operator switched off.</param>
    [Theory]
    [InlineData("XXX")]
    [InlineData("EUR")]
    public async Task AGuestOrderInACurrencyNotOnOfferIsRefused(string currency)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Handler().HandleAsync(GuestOrder(currency), CancellationToken.None));

        Assert.Empty(added);
    }

    /// <summary>An existing client already has a currency; the checkout's choice is ignored.</summary>
    [Fact]
    public async Task AnExistingClientsOrderIgnoresTheCommandsCurrency()
    {
        var existing = Client.Create(Subject, "Ada", "Lovelace", "ada@example.com");
        existing.SetCurrency("USD");
        caller.SetupGet(c => c.UserId).Returns(Subject);
        clients.Setup(r => r.FindByUserIdAsync(Subject, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        await Handler().HandleAsync(GuestOrder("AMD"), CancellationToken.None);

        Assert.Empty(added);
        Assert.Equal("USD", existing.Currency);
    }
}
