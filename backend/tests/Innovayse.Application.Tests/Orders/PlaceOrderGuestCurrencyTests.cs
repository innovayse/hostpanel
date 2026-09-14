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
using Innovayse.Domain.Orders;
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

    /// <summary>The USD monthly price of <see cref="HostingProduct"/>.</summary>
    private const decimal UsdMonthly = 19.99m;

    /// <summary>The AMD monthly price of <see cref="HostingProduct"/>; deliberately not a conversion of the USD one.</summary>
    private const decimal AmdMonthly = 7800m;

    /// <summary>The clients the handler adds, in order.</summary>
    private readonly List<Client> added = [];

    /// <summary>The invoices the handler adds, in order.</summary>
    private readonly List<Invoice> invoices = [];

    /// <summary>The orders the handler adds, in order.</summary>
    private readonly List<Order> orders = [];

    /// <summary>Every availability query the handler sent to the bus.</summary>
    private readonly List<ListAvailablePaymentMethodsQuery> availabilityQueries = [];

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

        // The order reads the per-currency price rows, not the legacy columns. Two distinct
        // figures, so a test can tell which currency a line was priced in.
        product.SetPrice("USD", BillingCycle.Monthly, UsdMonthly);
        product.SetPrice("AMD", BillingCycle.Monthly, AmdMonthly);

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
    /// A bus whose availability query lists the given modules — in every currency, or only in
    /// <paramref name="onlyInCurrency"/> when one is named, so a test can stand in for a gateway
    /// that takes one currency alone. The handler refuses an order for any module the checkout
    /// is not offering, so every harness has to offer the one its order names.
    /// </summary>
    private IMessageBus BusOffering(string? onlyInCurrency, params string[] modules)
    {
        var bus = new Mock<IMessageBus>();
        bus.Setup(b => b.InvokeAsync<IReadOnlyList<AvailablePaymentMethodDto>>(
                It.IsAny<ListAvailablePaymentMethodsQuery>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
            .Callback<object, CancellationToken, TimeSpan?>((q, _, _) => availabilityQueries.Add((ListAvailablePaymentMethodsQuery)q))
            .ReturnsAsync((object q, CancellationToken _, TimeSpan? _) =>
                onlyInCurrency is null || string.Equals(((ListAvailablePaymentMethodsQuery)q).CurrencyCode, onlyInCurrency, StringComparison.OrdinalIgnoreCase)
                    ? modules.Select(m => new AvailablePaymentMethodDto(m, m)).ToList()
                    : []);
        return bus.Object;
    }

    /// <summary>Builds the handler over the fixture world, with every method offered in every currency.</summary>
    /// <returns>The handler under test.</returns>
    private PlaceOrderHandler Handler() => Handler(BusOffering(null, "stripe"));

    /// <summary>Builds the handler over the fixture world and the given bus.</summary>
    /// <param name="bus">Answers the availability query.</param>
    /// <returns>The handler under test.</returns>
    private PlaceOrderHandler Handler(IMessageBus bus)
    {
        var products = new Mock<IProductRepository>();
        products.Setup(r => r.FindByIdsAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([HostingProduct()]);

        var orderRepo = new Mock<IOrderRepository>();
        orderRepo.Setup(r => r.GetNextOrderNumberAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        orderRepo.Setup(r => r.Add(It.IsAny<Order>())).Callback<Order>(orders.Add);

        var invoiceRepo = new Mock<IInvoiceRepository>();
        invoiceRepo.Setup(r => r.Add(It.IsAny<Invoice>())).Callback<Invoice>(invoices.Add);

        var provisioning = new Mock<IUserProvisioning>();
        provisioning.Setup(p => p.CreateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Subject);

        // The base is USD. An existing client is billed in whatever the resolver says their
        // currency is; it answers from the client record the way the real one does, so a client
        // this order created is billed in the currency that was recorded on them.
        var usd = Currency.Create("USD", "840", "$", string.Empty, 2, 1m, isBase: true);
        var payerCurrency = new Mock<IPayerCurrencyResolver>();
        payerCurrency.Setup(r => r.BaseAsync(It.IsAny<CancellationToken>())).ReturnsAsync(usd);
        payerCurrency.Setup(r => r.ForClientAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(async (int _, CancellationToken ct) =>
                added.Count == 0 ? usd : await currencies.Object.FindAsync(added[^1].Currency ?? "USD", ct) ?? usd);

        return new PlaceOrderHandler(
            orderRepo.Object,
            products.Object,
            clients.Object,
            invoiceRepo.Object,
            Mock.Of<IUnitOfWork>(),
            provisioning.Object,
            Mock.Of<ISubjectRoleStore>(),
            bus,
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

    /// <summary>
    /// The money path: a guest who chose AMD is charged the product's AMD price — not the USD
    /// figure, and not a conversion of it — and their invoice is issued in AMD.
    /// </summary>
    [Fact]
    public async Task AGuestOrderInAmdIsPricedAndInvoicedInAmd()
    {
        await Handler().HandleAsync(GuestOrder("AMD"), CancellationToken.None);

        var order = Assert.Single(orders);
        var line = Assert.Single(order.Items);
        Assert.Equal(AmdMonthly, line.FirstPaymentAmount);
        Assert.Equal(AmdMonthly, line.RecurringAmount);
        var invoice = Assert.Single(invoices);
        Assert.Equal("AMD", invoice.Currency);
        Assert.Equal(AmdMonthly, invoice.Total);
    }

    /// <summary>
    /// The payment-method check is made for the currency the guest chose, not for the base the
    /// caller — who has no client yet — would resolve to. A gateway that takes USD only is
    /// refused for an AMD order, and no account is created for it.
    /// </summary>
    [Fact]
    public async Task AGuestOrderInAmdIsRefusedAGatewayThatTakesUsdOnly()
    {
        var handler = Handler(BusOffering("USD", "stripe"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(GuestOrder("AMD"), CancellationToken.None));

        var query = Assert.Single(availabilityQueries);
        Assert.Equal("AMD", query.CurrencyCode);
        Assert.Empty(added);
        Assert.Empty(orders);
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
