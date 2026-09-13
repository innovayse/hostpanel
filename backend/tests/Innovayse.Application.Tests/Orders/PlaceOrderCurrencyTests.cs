namespace Innovayse.Application.Tests.Orders;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Billing.Queries.ListAvailablePaymentMethods;
using Innovayse.Application.Common;
using Innovayse.Application.Domains.Common;
using Innovayse.Application.Domains.Queries.GetTldPricing;
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

/// <summary>An order is priced from the stored price in the payer's currency — never converted.</summary>
/// <remarks>
/// The payer here is a signed-in client billed in AMD. Every product carries a USD price; whether
/// it also carries an AMD one decides whether the order goes through, and the amount the order
/// line records is the AMD figure exactly as stored, not the USD figure through any rate.
/// </remarks>
public sealed class PlaceOrderCurrencyTests
{
    /// <summary>The caller's subject.</summary>
    private const string Subject = "sso-subject-9";

    /// <summary>The product every order here is for.</summary>
    private const int ProductId = 3;

    /// <summary>The price the product carries in the base currency, which the payer is not billed in.</summary>
    private const decimal UsdMonthly = 10m;

    /// <summary>The price the product carries in the payer's currency.</summary>
    private const decimal AmdMonthly = 3900m;

    /// <summary>The currency the payer is billed in.</summary>
    private static readonly Currency Amd = Currency.Create("AMD", "051", string.Empty, " ֏", 0, 0.0025m, isBase: false);

    /// <summary>The orders the handler adds, in order.</summary>
    private readonly List<Order> orders = [];

    /// <summary>The product on offer; each test decides which currencies it sells in.</summary>
    private readonly Product product = HostingProduct();

    /// <summary>The TLD table the bus answers with; null when no test needs one.</summary>
    private TldPricingDto? tldPricing;

    /// <summary>Builds the product the order references, priced in USD only until a test says otherwise.</summary>
    /// <returns>An active hosting product carrying <see cref="ProductId"/>.</returns>
    private static Product HostingProduct()
    {
        var p = Product.Create(
            groupId: 1, name: "Pro Hosting", description: null, website: null, slug: null,
            packageName: null, type: ProductType.SharedHosting,
            monthlyPrice: 0m, annualPrice: 0m);
        p.SetPrice("USD", BillingCycle.Monthly, UsdMonthly);

        // Create leaves Id at 0 for EF to assign, and the handler looks the product up by id.
        typeof(Innovayse.Domain.Common.Entity)
            .GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(p, ProductId);

        return p;
    }

    /// <summary>A signed-in order for the given items.</summary>
    /// <param name="items">The order lines.</param>
    /// <returns>The command under test.</returns>
    private static PlaceOrderCommand Order(params PlaceOrderItemDto[] items) => new(
        FirstName: null, LastName: null, Email: null, Password: null, Phone: null,
        PaymentMethod: "stripe",
        Items: items);

    /// <summary>
    /// A bus that offers the "stripe" module and answers the TLD pricing query with
    /// <see cref="tldPricing"/>.
    /// </summary>
    /// <returns>The bus the handler dispatches through.</returns>
    private IMessageBus Bus()
    {
        var bus = new Mock<IMessageBus>();
        bus.Setup(b => b.InvokeAsync<IReadOnlyList<AvailablePaymentMethodDto>>(
                It.IsAny<ListAvailablePaymentMethodsQuery>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync([new AvailablePaymentMethodDto("stripe", "stripe")]);
        bus.Setup(b => b.InvokeAsync<TldPricingDto>(
                It.IsAny<GetTldPricingQuery>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(() => tldPricing!);
        return bus.Object;
    }

    /// <summary>Builds the handler over an AMD-billed, signed-in client.</summary>
    /// <returns>The handler under test.</returns>
    private PlaceOrderHandler Handler()
    {
        var client = Client.Create(Subject, "Ada", "Lovelace", "ada@example.com");
        client.SetCurrency("AMD");

        var clients = new Mock<IClientRepository>();
        clients.Setup(r => r.FindByUserIdAsync(Subject, It.IsAny<CancellationToken>())).ReturnsAsync(client);

        var caller = new Mock<ICurrentRequestContext>();
        caller.SetupGet(c => c.UserId).Returns(Subject);

        var products = new Mock<IProductRepository>();
        products.Setup(r => r.FindByIdsAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([product]);

        var orderRepo = new Mock<IOrderRepository>();
        orderRepo.Setup(r => r.GetNextOrderNumberAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        orderRepo.Setup(r => r.Add(It.IsAny<Order>())).Callback<Order>(orders.Add);

        var payerCurrency = new Mock<IPayerCurrencyResolver>();
        payerCurrency.Setup(r => r.ForClientAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(Amd);

        return new PlaceOrderHandler(
            orderRepo.Object,
            products.Object,
            clients.Object,
            Mock.Of<IInvoiceRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IUserProvisioning>(),
            Mock.Of<ISubjectRoleStore>(),
            Bus(),
            caller.Object,
            Mock.Of<IStringLocalizer<ValidationMessages>>(),
            payerCurrency.Object,
            Mock.Of<ICurrencyRepository>());
    }

    /// <summary>The AMD row is used for an AMD client; the USD row is never consulted.</summary>
    [Fact]
    public async Task Handle_UsesTheStoredPriceInThePayersCurrency()
    {
        product.SetPrice("AMD", BillingCycle.Monthly, AmdMonthly);

        await Handler().HandleAsync(Order(new PlaceOrderItemDto(ProductId, "monthly", null, null)), CancellationToken.None);

        var line = Assert.Single(Assert.Single(orders).Items);
        Assert.Equal(AmdMonthly, line.FirstPaymentAmount);
        Assert.Equal(AmdMonthly, line.RecurringAmount);
    }

    /// <summary>A product with no price in the payer's currency cannot be ordered by them.</summary>
    [Fact]
    public async Task Handle_RefusesAProductWithNoPriceInThatCurrency()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Handler().HandleAsync(Order(new PlaceOrderItemDto(ProductId, "monthly", null, null)), CancellationToken.None));

        Assert.Contains("AMD", ex.Message, StringComparison.Ordinal);
        Assert.Empty(orders);
    }

    /// <summary>A cycle the product does not price in the payer's currency is refused the same way.</summary>
    [Fact]
    public async Task Handle_RefusesACycleWithNoPriceInThatCurrency()
    {
        product.SetPrice("AMD", BillingCycle.Monthly, AmdMonthly);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Handler().HandleAsync(Order(new PlaceOrderItemDto(ProductId, "annual", null, null)), CancellationToken.None));

        Assert.Empty(orders);
    }

    /// <summary>
    /// TLD prices are still single-currency (their own plan follows). A domain whose sell currency
    /// is not the payer's is refused rather than billed in the wrong one.
    /// </summary>
    [Fact]
    public async Task Handle_RefusesADomainSoldInAnotherCurrency()
    {
        tldPricing = TldTable(sellCurrency: "USD", tableCurrency: "AMD");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Handler().HandleAsync(
                Order(new PlaceOrderItemDto(ProductId, "annual", "example.com", null, "register", null, 1)),
                CancellationToken.None));

        Assert.Contains("USD", ex.Message, StringComparison.Ordinal);
        Assert.Empty(orders);
    }

    /// <summary>A domain sold in the payer's currency is priced from its own table, unconverted.</summary>
    [Fact]
    public async Task Handle_PricesADomainSoldInThePayersCurrency()
    {
        tldPricing = TldTable(sellCurrency: "AMD", tableCurrency: "AMD");

        await Handler().HandleAsync(
            Order(new PlaceOrderItemDto(ProductId, "annual", "example.com", null, "register", null, 1)),
            CancellationToken.None);

        var line = Assert.Single(Assert.Single(orders).Items);
        Assert.Equal(4500m, line.FirstPaymentAmount);
    }

    /// <summary>A one-TLD pricing table for ".com", registering at 4500 for one year.</summary>
    /// <param name="sellCurrency">The currency the TLD's own prices are set in.</param>
    /// <param name="tableCurrency">The currency the table was asked for.</param>
    /// <returns>The table the bus answers with.</returns>
    private static TldPricingDto TldTable(string sellCurrency, string tableCurrency) => new(
        new TldCurrencyDto(tableCurrency, string.Empty),
        new Dictionary<string, TldPriceEntryDto>
        {
            ["com"] = new(
                sellCurrency,
                new Dictionary<string, string> { ["1"] = "4500.00" },
                new Dictionary<string, string> { ["1"] = "4500.00" },
                new Dictionary<string, string> { ["1"] = "4500.00" },
                []),
        });
}
