using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <c>POST /api/orders</c> with a <c>bundleId</c> against Postgres: the bundle's percentage and fixed discounts spread
/// over the lines, the included domain, the discount on top of a client group's, the refusals of lines that are not
/// the bundle's, and the bundle shown on the order's detail.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PlaceOrderBundleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Shared test password for every seeded test user.</summary>
    private const string TestPassword = "Pass@123!";

    /// <summary>The USD price of the bundle's first product.</summary>
    private const decimal HostingPrice = 40m;

    /// <summary>The USD price of the bundle's second product.</summary>
    private const decimal MailboxPrice = 10m;

    /// <summary>A bundle of two products at 10 percent bills every line a tenth cheaper and records the bundle.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PercentageBundle_BillsEveryLineAtTheDiscountedPriceAndRecordsTheBundle()
    {
        await OrdersTestSeeds.EnableBankTransferAsync(factory);
        var (hosting, mailbox) = await SeedTwoProductsAsync();
        var bundle = await CreateBundleAsync([hosting, mailbox], includesDomain: false, "Percentage", 10m);

        var response = await PlaceAsync(GuestBody(bundle.Id, [ProductLine(hosting), ProductLine(mailbox)]));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        using var placed = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        placed.RootElement.GetProperty("total").GetDecimal().Should().Be(45m);
        var order = await StoredOrderAsync(placed.RootElement);
        order.BundleId.Should().Be(bundle.Id);
        order.Items.OrderBy(i => i.Id).Select(i => i.FirstPaymentAmount).Should().Equal(36m, 9m);
        order.Items.OrderBy(i => i.Id).Select(i => i.RecurringAmount).Should().Equal(36m, 9m);
        var invoice = await StoredInvoiceAsync(placed.RootElement);
        invoice.Items.OrderBy(i => i.Id).Select(i => i.UnitPrice).Should().Equal(36m, 9m);
        invoice.Items.Should().OnlyContain(i => i.Description.EndsWith($"({bundle.Name})", StringComparison.Ordinal));
    }

    /// <summary>A fixed discount in the payer's currency is split in proportion to the prices.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FixedBundle_SplitsTheAmountInProportionToThePrices()
    {
        await OrdersTestSeeds.EnableBankTransferAsync(factory);
        var (hosting, mailbox) = await SeedTwoProductsAsync();
        var bundle = await CreateBundleAsync([hosting, mailbox], includesDomain: false, "Fixed", 10m, "USD");

        var response = await PlaceAsync(GuestBody(bundle.Id, [ProductLine(hosting), ProductLine(mailbox)]));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        using var placed = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        placed.RootElement.GetProperty("total").GetDecimal().Should().Be(40m);
        var invoice = await StoredInvoiceAsync(placed.RootElement);
        invoice.Items.OrderBy(i => i.Id).Select(i => i.UnitPrice).Should().Equal(32m, 8m);
    }

    /// <summary>
    /// A fixed discount stated in another currency than the payer's is converted with the stored rates before it is
    /// taken off, so an AMD payer is given the AMD worth of the USD amount.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FixedBundle_InAnotherCurrency_IsConvertedToThePayersCurrency()
    {
        await OrdersTestSeeds.EnableBankTransferAsync(factory);
        await BillingTestSeeds.EnsureCurrencyAsync(factory, "USD", "840", 1m);
        await BillingTestSeeds.EnsureCurrencyAsync(factory, "AMD", "051", 0.0026m);
        var productId = await SeedAmdProductAsync(20000m);
        var bundle = await CreateBundleAsync([productId], includesDomain: false, "Fixed", 10m, "USD");
        var discount = await ConvertedAsync(10m, "USD", "AMD");
        var email = $"guest-{Guid.NewGuid():N}@example.test";

        var response = await PlaceAsync(GuestBody(bundle.Id, [ProductLine(productId)], currency: "AMD", email: email));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        using var placed = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        placed.RootElement.GetProperty("currency").GetString().Should().Be("AMD");
        discount.Should().BeGreaterThan(0m);
        placed.RootElement.GetProperty("total").GetDecimal().Should().Be(20000m - discount);
        (await GuestAccountExistsAsync(email)).Should().BeTrue();
    }

    /// <summary>
    /// A fixed discount in a currency this panel does not store is refused before a guest account exists, instead of
    /// being taken off unconverted.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FixedBundle_InAnUnstoredCurrency_IsRefusedBeforeAnythingIsCreated()
    {
        await OrdersTestSeeds.EnableBankTransferAsync(factory);
        var hosting = await ProductsTestSeeds.SeedProductWithUsdPricesAsync(factory, (BillingCycle.Monthly, HostingPrice));
        var bundle = await CreateBundleAsync([hosting], includesDomain: false, "Fixed", 10m, "XTS");
        var email = $"guest-{Guid.NewGuid():N}@example.test";

        var response = await PlaceAsync(GuestBody(bundle.Id, [ProductLine(hosting)], email: email));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_BUNDLE_DISCOUNT_CURRENCY_UNAVAILABLE);
        (await OrdersOfBundleAsync(bundle.Id)).Should().Be(0);
        (await GuestAccountExistsAsync(email)).Should().BeFalse();
    }

    /// <summary>
    /// A bundle with a domain takes the domain line too: the line is discounted with the rest, and its period is the
    /// bundle's, whatever the checkout sent (the five years sent here have no price, the bundle's one year has).
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task BundleWithADomain_PricesTheDomainLineForTheBundlesYears()
    {
        await OrdersTestSeeds.EnableBankTransferAsync(factory);
        var hosting = await ProductsTestSeeds.SeedProductWithUsdPricesAsync(factory, (BillingCycle.Monthly, HostingPrice));
        var tld = $"x{Guid.NewGuid():N}"[..9];
        await OrdersTestSeeds.SeedUsdTldAndRatesAsync(factory, tld, 10m);
        var domainProduct = await OrdersTestSeeds.SeedDomainProductAsync(factory);
        var bundle = await CreateBundleAsync([hosting], includesDomain: true, "Percentage", 10m);

        var response = await PlaceAsync(GuestBody(bundle.Id, [ProductLine(hosting), DomainLine(domainProduct, $"example.{tld}")]));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        using var placed = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        placed.RootElement.GetProperty("total").GetDecimal().Should().Be(45m);
        var order = await StoredOrderAsync(placed.RootElement);
        var domainItem = order.Items.Single(i => i.Domain is not null);
        domainItem.Years.Should().Be(1);
        domainItem.FirstPaymentAmount.Should().Be(9m);
        var invoice = await StoredInvoiceAsync(placed.RootElement);
        invoice.Items.Should().Contain(i => i.Description == $"Domain Registration (example.{tld}) ({bundle.Name})");
    }

    /// <summary>
    /// A client in a group with a discount keeps it inside a bundle: the bundle's discount comes off the already
    /// group-discounted price, and the invoice line still records the group's percent.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task BundleDiscount_ComesOnTopOfTheClientGroupDiscount()
    {
        await OrdersTestSeeds.EnableBankTransferAsync(factory);
        var productId = await ProductsTestSeeds.SeedProductWithUsdPricesAsync(factory, (BillingCycle.Monthly, HostingPrice));
        var bundle = await CreateBundleAsync([productId], includesDomain: false, "Percentage", 10m);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var groupId = await ClientGroupTestSeeds.CreateAsync(admin, discountPercent: 25m);
        var (caller, clientId) = await ClientsTestSeeds.RegisterClientAsync(factory, "bundle-group");
        await ClientGroupTestSeeds.AssignAsync(factory, clientId, groupId);

        var response = await caller.PostAsJsonAsync("/api/orders", new
        {
            paymentMethod = "bank_transfer",
            bundleId = bundle.Id,
            items = new[] { ProductLine(productId) },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        using var placed = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        placed.RootElement.GetProperty("total").GetDecimal().Should().Be(27m);
        var invoice = await StoredInvoiceAsync(placed.RootElement);
        var line = invoice.Items.Single();
        line.UnitPrice.Should().Be(27m);
        line.DiscountPercent.Should().Be(25m);
    }

    /// <summary>A bundle order missing one of the bundle's products is refused and creates no order.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OrderMissingAComponent_IsRefused()
    {
        await OrdersTestSeeds.EnableBankTransferAsync(factory);
        var (hosting, mailbox) = await SeedTwoProductsAsync();
        var bundle = await CreateBundleAsync([hosting, mailbox], includesDomain: false, "Percentage", 10m);

        var response = await PlaceAsync(GuestBody(bundle.Id, [ProductLine(hosting)]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_BUNDLE_ITEMS_MISMATCH);
        (await OrdersOfBundleAsync(bundle.Id)).Should().Be(0);
    }

    /// <summary>A bundle that includes a domain refuses an order with no domain line.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OrderWithoutTheIncludedDomain_IsRefused()
    {
        await OrdersTestSeeds.EnableBankTransferAsync(factory);
        var hosting = await ProductsTestSeeds.SeedProductWithUsdPricesAsync(factory, (BillingCycle.Monthly, HostingPrice));
        var bundle = await CreateBundleAsync([hosting], includesDomain: true, "Percentage", 10m);

        var email = $"guest-{Guid.NewGuid():N}@example.test";

        var response = await PlaceAsync(GuestBody(bundle.Id, [ProductLine(hosting)], email: email));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_BUNDLE_DOMAIN_MISMATCH);
        (await OrdersOfBundleAsync(bundle.Id)).Should().Be(0);
        (await GuestAccountExistsAsync(email)).Should().BeFalse();
    }

    /// <summary>A renewal is not something a bundle includes.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OrderRenewingTheDomain_IsRefused()
    {
        await OrdersTestSeeds.EnableBankTransferAsync(factory);
        var hosting = await ProductsTestSeeds.SeedProductWithUsdPricesAsync(factory, (BillingCycle.Monthly, HostingPrice));
        var domainProduct = await OrdersTestSeeds.SeedDomainProductAsync(factory);
        var bundle = await CreateBundleAsync([hosting], includesDomain: true, "Percentage", 10m);

        var response = await PlaceAsync(GuestBody(
            bundle.Id, [ProductLine(hosting), DomainLine(domainProduct, "example.com", nameof(DomainAction.Renew))]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_BUNDLE_DOMAIN_ACTION_NOT_ALLOWED);
        (await OrdersOfBundleAsync(bundle.Id)).Should().Be(0);
    }

    /// <summary>An inactive bundle may not be ordered.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InactiveBundle_IsRefused()
    {
        await OrdersTestSeeds.EnableBankTransferAsync(factory);
        var hosting = await ProductsTestSeeds.SeedProductWithUsdPricesAsync(factory, (BillingCycle.Monthly, HostingPrice));
        var bundle = await CreateBundleAsync([hosting], includesDomain: false, "Percentage", 10m);
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        var body = BundleBody([hosting], includesDomain: false, "Percentage", 10m, null);
        body["isActive"] = false;
        (await admin.PutAsJsonAsync($"/api/products/bundles/{bundle.Id}", body)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await PlaceAsync(GuestBody(bundle.Id, [ProductLine(hosting)]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_BUNDLE_NOT_AVAILABLE);
        (await OrdersOfBundleAsync(bundle.Id)).Should().Be(0);
    }

    /// <summary>A bundle id that names no bundle answers 404.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownBundle_AnswersNotFound()
    {
        await OrdersTestSeeds.EnableBankTransferAsync(factory);
        var hosting = await ProductsTestSeeds.SeedProductWithUsdPricesAsync(factory, (BillingCycle.Monthly, HostingPrice));

        var response = await PlaceAsync(GuestBody(int.MaxValue, [ProductLine(hosting)]));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_BUNDLE_NOT_FOUND);
    }

    /// <summary>The order's detail names the bundle it was placed from, and an ordinary order names none.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OrderDetail_ShowsTheBundle_AndNullForAnOrdinaryOrder()
    {
        await OrdersTestSeeds.EnableBankTransferAsync(factory);
        var hosting = await ProductsTestSeeds.SeedProductWithUsdPricesAsync(factory, (BillingCycle.Monthly, HostingPrice));
        var bundle = await CreateBundleAsync([hosting], includesDomain: false, "Percentage", 10m);
        var placed = await PlaceAsync(GuestBody(bundle.Id, [ProductLine(hosting)]));
        placed.StatusCode.Should().Be(HttpStatusCode.Created, await placed.Content.ReadAsStringAsync());
        var orderId = (await placed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderId").GetInt32();
        var ordinaryId = await OrdersTestSeeds.SeedOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}");
        var ordinary = await admin.GetFromJsonAsync<JsonElement>($"/api/orders/{ordinaryId}");

        detail.GetProperty("bundleId").GetInt32().Should().Be(bundle.Id);
        detail.GetProperty("bundleName").GetString().Should().Be(bundle.Name);
        ordinary.TryGetProperty("bundleId", out var none).Should().BeTrue();
        none.ValueKind.Should().Be(JsonValueKind.Null);
        ordinary.GetProperty("bundleName").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>Seeds the bundle's two USD products, <see cref="HostingPrice"/> and <see cref="MailboxPrice"/> a month.</summary>
    /// <returns>The two product ids, in that order.</returns>
    private async Task<(int Hosting, int Mailbox)> SeedTwoProductsAsync()
    {
        var hosting = await ProductsTestSeeds.SeedProductWithUsdPricesAsync(factory, (BillingCycle.Monthly, HostingPrice));
        var mailbox = await ProductsTestSeeds.SeedProductWithUsdPricesAsync(factory, (BillingCycle.Monthly, MailboxPrice));
        return (hosting, mailbox);
    }

    /// <summary>Seeds an active hosting product priced in AMD only.</summary>
    /// <param name="amdMonthly">The AMD monthly price.</param>
    /// <returns>The new product's primary key.</returns>
    private async Task<int> SeedAmdProductAsync(decimal amdMonthly)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var group = ProductGroup.Create($"bundle-amd-{Guid.NewGuid():N}", null);
        db.ProductGroups.Add(group);
        await db.SaveChangesAsync();

        var product = Product.Create(
            group.Id, "AMD Hosting", null, null, null, null, ProductType.SharedHosting, 0m, 0m, DateTimeOffset.UtcNow);
        product.SetPrice("AMD", BillingCycle.Monthly, amdMonthly);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    /// <summary>Builds a bundle create body: the shared one with the fields under test overridden.</summary>
    /// <param name="productIds">The component products; the first is the primary one.</param>
    /// <param name="includesDomain">Whether the bundle includes a one year domain registration.</param>
    /// <param name="discountType">The discount type name.</param>
    /// <param name="discountAmount">The discount amount.</param>
    /// <param name="discountCurrency">The currency of a fixed discount, or <see langword="null"/>.</param>
    /// <returns>The body.</returns>
    private static Dictionary<string, object?> BundleBody(
        int[] productIds, bool includesDomain, string discountType, decimal discountAmount, string? discountCurrency)
    {
        var body = ProductsTestSeeds.BundleBody(productIds);
        body["includesDomain"] = includesDomain;
        body["domainYears"] = includesDomain ? 1 : null;
        body["discountType"] = discountType;
        body["discountAmount"] = discountAmount;
        body["discountCurrency"] = discountCurrency;
        return body;
    }

    /// <summary>Creates a bundle through the admin API.</summary>
    /// <param name="productIds">The component products; the first is the primary one.</param>
    /// <param name="includesDomain">Whether the bundle includes a one year domain registration.</param>
    /// <param name="discountType">The discount type name.</param>
    /// <param name="discountAmount">The discount amount.</param>
    /// <param name="discountCurrency">The currency of a fixed discount, or <see langword="null"/>.</param>
    /// <returns>The new bundle's id and name.</returns>
    private async Task<(int Id, string Name)> CreateBundleAsync(
        int[] productIds, bool includesDomain, string discountType, decimal discountAmount, string? discountCurrency = null)
    {
        var body = BundleBody(productIds, includesDomain, discountType, discountAmount, discountCurrency);
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        return (await ProductsTestSeeds.CreateBundleAsync(admin, body), (string)body["name"]!);
    }

    /// <summary>Builds a guest checkout body for a bundle order.</summary>
    /// <param name="bundleId">The bundle ordered.</param>
    /// <param name="items">The order's lines.</param>
    /// <param name="currency">The currency the guest chooses, or <see langword="null"/> for the base.</param>
    /// <param name="email">The guest's address, or <see langword="null"/> for a fresh one.</param>
    /// <returns>The request body.</returns>
    private static object GuestBody(int bundleId, object[] items, string? currency = null, string? email = null) => new
    {
        firstName = "Ada",
        lastName = "Lovelace",
        email = email ?? $"guest-{Guid.NewGuid():N}@example.test",
        password = TestPassword,
        paymentMethod = "bank_transfer",
        currency,
        bundleId,
        items,
    };

    /// <summary>Builds a monthly product line.</summary>
    /// <param name="productId">The product ordered.</param>
    /// <returns>The line.</returns>
    private static object ProductLine(int productId) => new { productId, billingCycle = nameof(BillingCycle.Monthly) };

    /// <summary>Builds a domain line that asks for five years, which the bundle overrides with its own.</summary>
    /// <param name="productId">The domain product.</param>
    /// <param name="domain">The domain name.</param>
    /// <param name="action">The domain action name.</param>
    /// <returns>The line.</returns>
    private static object DomainLine(int productId, string domain, string action = nameof(DomainAction.Register)) => new
    {
        productId,
        billingCycle = nameof(BillingCycle.Annual),
        domain,
        domainAction = action,
        years = 5,
    };

    /// <summary>Posts a checkout body anonymously.</summary>
    /// <param name="body">The request body.</param>
    /// <returns>The response.</returns>
    private Task<HttpResponseMessage> PlaceAsync(object body) => factory.CreateClient().PostAsJsonAsync("/api/orders", body);

    /// <summary>Reads the stored order a checkout answered with, with its lines.</summary>
    /// <param name="placed">The checkout's answer.</param>
    /// <returns>The order.</returns>
    private async Task<Order> StoredOrderAsync(JsonElement placed)
    {
        var orderId = placed.GetProperty("orderId").GetInt32();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Orders.AsNoTracking().Include(o => o.Items).SingleAsync(o => o.Id == orderId);
    }

    /// <summary>Reads the stored invoice a checkout answered with, with its lines.</summary>
    /// <param name="placed">The checkout's answer.</param>
    /// <returns>The invoice.</returns>
    private async Task<Invoice> StoredInvoiceAsync(JsonElement placed)
    {
        var invoiceId = placed.GetProperty("invoiceId").GetInt32();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Invoices.AsNoTracking().Include(i => i.Items).SingleAsync(i => i.Id == invoiceId);
    }

    /// <summary>Whether guest checkout created an account for an address.</summary>
    /// <param name="email">The guest's address.</param>
    /// <returns><see langword="true"/> when an account exists for it.</returns>
    private async Task<bool> GuestAccountExistsAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.AsNoTracking().AnyAsync(u => u.Email == email);
    }

    /// <summary>Counts the orders placed from a bundle.</summary>
    /// <param name="bundleId">The bundle.</param>
    /// <returns>How many orders carry it.</returns>
    private async Task<int> OrdersOfBundleAsync(int bundleId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Orders.AsNoTracking().CountAsync(o => o.BundleId == bundleId);
    }

    /// <summary>Converts an amount with the stored rates, the way the order flow does.</summary>
    /// <param name="amount">The amount to convert.</param>
    /// <param name="from">The ISO 4217 code of its currency.</param>
    /// <param name="to">The ISO 4217 code of the currency to convert to.</param>
    /// <returns>The converted, rounded amount.</returns>
    private async Task<decimal> ConvertedAsync(decimal amount, string from, string to)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.Currencies.AsNoTracking().ToListAsync();
        return rows.Single(c => c.Code == from).ConvertTo(amount, rows.Single(c => c.Code == to));
    }
}
