using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Auth.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Auth.Enums;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <c>POST /api/orders</c> against Postgres: guest checkout, an already signed-in caller's first
/// order, and the checkout-time refusals (<c>ORDER_PAYMENT_METHOD_UNAVAILABLE</c>,
/// <c>ORDER_CURRENCY_UNAVAILABLE</c>, <c>PRODUCT_NOT_FOUND</c>,
/// <c>ORDER_PRODUCT_PRICE_UNAVAILABLE</c>) and the validator's shape refusal on an unparseable
/// billing cycle.
/// </summary>
/// <remarks>
/// This suite drives the scenarios through the real endpoint, the real database and the real
/// bank-transfer on/off switch; nothing here is faked.
/// <para>
/// The billing-cycle refusal and the <c>PRODUCT_NOT_FOUND</c>/<c>ORDER_PRODUCT_PRICE_UNAVAILABLE</c>
/// refusals below go through the same endpoint. <c>ORDER_GUEST_DETAILS_REQUIRED</c> is not tested here: the
/// validator already requires every guest field before the command reaches the handler (see
/// <c>PlaceOrderValidator</c>), so no HTTP request can reach the handler's own check of it; the
/// validator's requirement is exercised indirectly by every guest test below sending complete
/// fields.
/// </para>
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PlaceOrderTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Shared test password for every seeded test user.</summary>
    private const string TestPassword = "Pass@123!";

    /// <summary>The USD monthly price every seeded product carries.</summary>
    private const decimal UsdMonthly = 19.99m;

    /// <summary>An admin-authenticated client.</summary>
    /// <returns>The client.</returns>
    private async Task<HttpClient> AdminClientAsync()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());
        return client;
    }

    /// <summary>Switches the bank-transfer integration on through the real admin endpoint.</summary>
    /// <remarks>
    /// Checkout offers nothing a fresh migration has not switched on, and <c>PlaceOrderHandler</c>
    /// refuses any module the checkout is not offering, so a test placing an order has to turn
    /// the method on first, exactly as an operator would.
    /// </remarks>
    /// <returns>A task that completes once the switch is on.</returns>
    private async Task EnableBankTransferAsync()
    {
        var admin = await AdminClientAsync();
        var response = await admin.PutAsJsonAsync(
            "/api/admin/integrations/bank-transfer",
            new { isEnabled = true, config = new Dictionary<string, string>() });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Seeds an active hosting product priced in USD (and, optionally, AMD).</summary>
    /// <param name="amdMonthly">The AMD price to set, or <see langword="null"/> for USD only.</param>
    /// <param name="name">The product's name, which the invoice line carries.</param>
    /// <param name="usdMonthly">The USD monthly price; <see cref="UsdMonthly"/> when omitted.</param>
    /// <param name="type">The product's type; shared hosting when omitted.</param>
    /// <param name="sku">The product's SKU, or <see langword="null"/> for none.</param>
    /// <param name="metadata">The product's metadata as JSON object text, or <see langword="null"/> for none.</param>
    /// <returns>The new product's primary key.</returns>
    private async Task<int> SeedProductAsync(
        decimal? amdMonthly = null, string name = "Starter Hosting", decimal usdMonthly = UsdMonthly,
        ProductType type = ProductType.SharedHosting, string? sku = null, string? metadata = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var group = ProductGroup.Create($"place-order-{Guid.NewGuid():N}", null);
        db.ProductGroups.Add(group);
        await db.SaveChangesAsync();

        var product = Product.Create(
            group.Id, name, null, null, null, null, type, 0m, 0m, DateTimeOffset.UtcNow);
        product.SetSku(sku);
        product.SetMetadata(metadata);
        product.SetPrice("USD", BillingCycle.Monthly, usdMonthly);
        if (amdMonthly.HasValue)
        {
            product.SetPrice("AMD", BillingCycle.Monthly, amdMonthly.Value);
        }

        db.Products.Add(product);
        await db.SaveChangesAsync();

        return product.Id;
    }

    /// <summary>Seeds an active hosting product that carries no price at all, in any currency.</summary>
    /// <returns>The new product's primary key.</returns>
    private async Task<int> SeedProductWithNoPriceAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var group = ProductGroup.Create($"place-order-no-price-{Guid.NewGuid():N}", null);
        db.ProductGroups.Add(group);
        await db.SaveChangesAsync();

        var product = Product.Create(
            group.Id, "Priceless Hosting", null, null, null, null, ProductType.SharedHosting, 0m, 0m, DateTimeOffset.UtcNow);

        db.Products.Add(product);
        await db.SaveChangesAsync();

        return product.Id;
    }

    /// <summary>
    /// A guest checkout with valid registration fields places an order, gets a payment token, and
    /// keeps the phone number the guest gave on the client record it creates.
    /// </summary>
    [Fact]
    public async Task GuestCheckoutPlacesAnOrderAndReturnsAPaymentToken()
    {
        await EnableBankTransferAsync();
        var productId = await SeedProductAsync();
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            email = $"guest-{Guid.NewGuid():N}@example.test",
            password = TestPassword,
            phoneNumber = "+37400000000",
            paymentMethod = "bank_transfer",
            items = new[] { new { productId, billingCycle = nameof(BillingCycle.Monthly) } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("orderId").GetInt32().Should().BeGreaterThan(0);
        body.RootElement.GetProperty("invoiceId").GetInt32().Should().BeGreaterThan(0);
        body.RootElement.GetProperty("paymentToken").GetString().Should().NotBeNullOrWhiteSpace();
        // The backend states what it billed, so the checkout can compare it with the total it showed.
        body.RootElement.GetProperty("total").GetDecimal().Should().Be(UsdMonthly);
        body.RootElement.GetProperty("currency").GetString().Should().Be("USD");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orderId = body.RootElement.GetProperty("orderId").GetInt32();
        var clientId = await db.Orders.AsNoTracking().Where(o => o.Id == orderId).Select(o => o.ClientId).SingleAsync();
        var stored = await db.Clients.AsNoTracking().SingleAsync(c => c.Id == clientId);
        stored.Phone.Should().Be("+37400000000");
    }

    /// <summary>
    /// A priced order is left for the payer to pay: it bills its full total, its invoice stays
    /// Unpaid and the order stays Pending. Only an order that owes nothing is settled at checkout.
    /// </summary>
    [Fact]
    public async Task PricedOrderIsNotSettledAtCheckout()
    {
        await EnableBankTransferAsync();
        var productId = await SeedProductAsync();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/orders", GuestOrder(productId));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("total").GetDecimal().Should().Be(UsdMonthly);
        var orderId = body.RootElement.GetProperty("orderId").GetInt32();
        var invoiceId = body.RootElement.GetProperty("invoiceId").GetInt32();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId)).Status.Should().Be(InvoiceStatus.Unpaid);
        (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId)).Status.Should().Be(OrderStatus.Pending);
    }

    /// <summary>
    /// Orders placed at the same moment each get their own number and none fails. A number taken as
    /// the stored maximum plus one would collide under concurrency, and the unique index would
    /// refuse all but one with a server error.
    /// </summary>
    [Fact]
    public async Task ConcurrentOrdersAreNumberedDistinctlyAndNoneFails()
    {
        // Arrange
        const int Parallel = 12;
        await EnableBankTransferAsync();
        var productId = await SeedProductAsync();

        // Act: every request starts behind one gate so they overlap as much as the host allows.
        var gate = new TaskCompletionSource();
        var requests = Enumerable.Range(0, Parallel).Select(async _ =>
        {
            await gate.Task;
            return await factory.CreateClient().PostAsJsonAsync("/api/orders", new
            {
                firstName = "Ada",
                lastName = "Lovelace",
                email = $"guest-{Guid.NewGuid():N}@example.test",
                password = TestPassword,
                paymentMethod = "bank_transfer",
                items = new[] { new { productId, billingCycle = nameof(BillingCycle.Monthly) } },
            });
        }).ToList();
        gate.SetResult();
        var responses = await Task.WhenAll(requests);

        // Assert: all created, all numbers different.
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created);
        var orderIds = new List<int>();
        foreach (var response in responses)
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            orderIds.Add(body.RootElement.GetProperty("orderId").GetInt32());
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var numbers = await db.Orders.Where(o => orderIds.Contains(o.Id)).Select(o => o.OrderNumber).ToListAsync();
        numbers.Should().HaveCount(Parallel);
        numbers.Should().OnlyHaveUniqueItems();
        numbers.Should().OnlyContain(n => n.StartsWith("ORD-"));
    }

    /// <summary>A guest checkout naming a currency this panel does not offer is refused before an account exists.</summary>
    [Fact]
    public async Task GuestCheckoutRefusesAnUnconfiguredCurrency()
    {
        await EnableBankTransferAsync();
        var productId = await SeedProductAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            email = $"guest-{Guid.NewGuid():N}@example.test",
            password = TestPassword,
            paymentMethod = "bank_transfer",
            currency = "ZZZ",
            items = new[] { new { productId, billingCycle = nameof(BillingCycle.Monthly) } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertCodeAsync(response, HostpanelErrorCodes.ORDER_CURRENCY_UNAVAILABLE);
    }

    /// <summary>A checkout naming a payment method nobody switched on is refused, and no order is created.</summary>
    [Fact]
    public async Task CheckoutRefusesAPaymentMethodNotOnOffer()
    {
        var productId = await SeedProductAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            email = $"guest-{Guid.NewGuid():N}@example.test",
            password = TestPassword,
            // A method no test ever switches on: sibling tests enable bank_transfer in the shared
            // host, so naming it here made this refusal depend on test order.
            paymentMethod = "never_enabled_method",
            items = new[] { new { productId, billingCycle = nameof(BillingCycle.Monthly) } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertCodeAsync(response, HostpanelErrorCodes.ORDER_PAYMENT_METHOD_UNAVAILABLE);
    }

    /// <summary>
    /// A signed-in caller placing their first order gets a client record rather than a refusal:
    /// this flow has no "no client account" refusal.
    /// </summary>
    [Fact]
    public async Task SignedInCallersFirstOrderCreatesTheirClientRecordAndSucceeds()
    {
        await EnableBankTransferAsync();
        var productId = await SeedProductAsync();

        var email = $"client-{Guid.NewGuid():N}@example.test";
        var register = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = TestPassword,
            firstName = "Grace",
            lastName = "Hopper",
        });
        register.StatusCode.Should().Be(HttpStatusCode.OK);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await factory.GetClientTokenAsync(email, TestPassword));

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            paymentMethod = "bank_transfer",
            items = new[] { new { productId, billingCycle = nameof(BillingCycle.Monthly) } },
        });

        // The body rides along as the reason: a bare 500 says nothing about which side failed.
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        // The new client row is only reachable through the Client role: without it every portal
        // call answers 403 and the portal tells a paying customer they are not one. An SSO
        // sign-up holds no Client role until this order grants it.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        using var placed = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var orderId = placed.RootElement.GetProperty("orderId").GetInt32();
        var clientId = (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId)).ClientId;
        var subject = (await db.Clients.AsNoTracking().SingleAsync(c => c.Id == clientId)).UserId;
        (await db.SubjectRoles.AsNoTracking().AnyAsync(r => r.Subject == subject && r.Role == Role.Client))
            .Should().BeTrue();
    }

    /// <summary>
    /// A signed-in caller whose client row already exists but who holds no Client role (a sign-up
    /// through the external sign-on, whose row the registration handler wrote first) is granted
    /// the role by their order. Without it every portal call answered 403 after they had ordered.
    /// </summary>
    [Fact]
    public async Task SignedInCallerWithAClientRowButNoRoleIsGrantedTheClientRoleByTheirOrder()
    {
        await EnableBankTransferAsync();
        var productId = await SeedProductAsync();
        var subject = await ClientsTestSeeds.CreateUnlinkedSubjectAsync(factory, "orderer-no-role");
        using (var seed = factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Clients.Add(Domain.Clients.Client.Create(subject.UserId, "Fresh", "Subject", subject.Email, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
            (await seed.ServiceProvider.GetRequiredService<ISubjectRoleStore>()
                .GetRolesAsync(subject.UserId, CancellationToken.None)).Should().NotContain(Role.Client);
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await factory.GetClientTokenAsync(subject.Email, TestPassword));

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            paymentMethod = "bank_transfer",
            items = new[] { new { productId, billingCycle = nameof(BillingCycle.Monthly) } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ISubjectRoleStore>()
            .GetRolesAsync(subject.UserId, CancellationToken.None)).Should().Contain(Role.Client);
    }

    /// <summary>
    /// A credential whose display name is only an email address (a sign-on provider that knows no
    /// name sends the address instead) is not stored as the first and last name. The new client row
    /// is named after the email's local part, and the caller is granted the Client role.
    /// </summary>
    [Fact]
    public async Task SignedInCallerWhoseDisplayNameIsAnEmailIsNotNamedAfterTheWholeAddress()
    {
        await EnableBankTransferAsync();
        var productId = await SeedProductAsync();
        var email = $"mail-as-name-{Guid.NewGuid():N}@example.test";
        string userId;
        using (var seed = factory.Services.CreateScope())
        {
            userId = await seed.ServiceProvider.GetRequiredService<IUserService>().CreateAsync(
                new CreateUserInput(email, TestPassword, email, null), CancellationToken.None);
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await factory.GetClientTokenAsync(email, TestPassword));

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            paymentMethod = "bank_transfer",
            items = new[] { new { productId, billingCycle = nameof(BillingCycle.Monthly) } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Clients
            .AsNoTracking()
            .SingleAsync(c => c.UserId == userId);
        stored.FirstName.Should().Be(email.Split('@')[0]);
        stored.LastName.Should().BeEmpty();
        (await scope.ServiceProvider.GetRequiredService<ISubjectRoleStore>()
            .GetRolesAsync(userId, CancellationToken.None)).Should().Contain(Role.Client);
    }

    /// <summary>A checkout naming a product id nothing in the catalogue carries is refused.</summary>
    [Fact]
    public async Task CheckoutRefusesAProductThatDoesNotExist()
    {
        await EnableBankTransferAsync();
        const int MissingProductId = 987_654_321;
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            email = $"guest-{Guid.NewGuid():N}@example.test",
            password = TestPassword,
            paymentMethod = "bank_transfer",
            items = new[] { new { productId = MissingProductId, billingCycle = nameof(BillingCycle.Monthly) } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_NOT_FOUND);
    }

    /// <summary>A checkout for a product that carries no price at all is refused rather than billed for free.</summary>
    [Fact]
    public async Task CheckoutRefusesAProductWithNoPriceForTheRequestedCurrency()
    {
        await EnableBankTransferAsync();
        var productId = await SeedProductWithNoPriceAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            email = $"guest-{Guid.NewGuid():N}@example.test",
            password = TestPassword,
            paymentMethod = "bank_transfer",
            items = new[] { new { productId, billingCycle = nameof(BillingCycle.Monthly) } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertCodeAsync(response, HostpanelErrorCodes.ORDER_PRODUCT_PRICE_UNAVAILABLE);
    }

    /// <summary>
    /// A billing cycle that is not one of the enum's names is refused as a 400 before the handler
    /// runs, including the legacy "annually" spelling the wire no longer accepts.
    /// </summary>
    /// <param name="cycle">The cycle text sent in the order line.</param>
    [Theory]
    [InlineData("weekly")]
    [InlineData("annually")]
    public async Task CheckoutRefusesABillingCycleThatIsNotAnEnumName(string cycle)
    {
        await EnableBankTransferAsync();
        var productId = await SeedProductAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            email = $"guest-{Guid.NewGuid():N}@example.test",
            password = TestPassword,
            paymentMethod = "bank_transfer",
            items = new[] { new { productId, billingCycle = cycle } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// A payer billed in AMD can order a domain whose TLD is sold in USD: the line is priced with
    /// the stored exchange rates and the invoice is raised in AMD for the converted amount.
    /// </summary>
    [Fact]
    public async Task GuestCheckoutOrdersADomainOfAUsdTldInTheChosenCurrency()
    {
        await EnableBankTransferAsync();
        var tld = $"x{Guid.NewGuid():N}"[..9];
        const decimal usdSellPrice = 10m;
        var expectedAmd = await SeedUsdTldAndRatesAsync(tld, usdSellPrice);
        var productId = await SeedDomainProductAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            email = $"guest-{Guid.NewGuid():N}@example.test",
            password = TestPassword,
            paymentMethod = "bank_transfer",
            currency = "AMD",
            items = new[]
            {
                new
                {
                    productId,
                    billingCycle = nameof(BillingCycle.Annual),
                    domain = $"example.{tld}",
                    domainAction = nameof(DomainAction.Register),
                    years = 1,
                },
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var invoiceId = body.RootElement.GetProperty("invoiceId").GetInt32();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invoice = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId);
        invoice.Currency.Should().Be("AMD");
        var orderCurrency = await db.Orders.AsNoTracking()
            .Where(o => o.InvoiceId == invoiceId)
            .Select(o => o.Currency)
            .SingleAsync();
        orderCurrency.Should().Be("AMD");
        invoice.SubTotal.Should().Be(expectedAmd);
        expectedAmd.Should().NotBe(usdSellPrice);
    }

    /// <summary>
    /// A fractional domain price is read the same under every request language: a checkout sent
    /// in Russian, whose culture writes a decimal comma, still orders the domain at 9.99.
    /// </summary>
    [Fact]
    public async Task DomainPriceIsReadTheSameUnderEveryRequestLanguage()
    {
        await EnableBankTransferAsync();
        var tld = $"x{Guid.NewGuid():N}"[..9];
        const decimal usdSellPrice = 9.99m;
        await SeedUsdTldAndRatesAsync(tld, usdSellPrice);
        var productId = await SeedDomainProductAsync();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Language", "ru");

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            email = $"guest-{Guid.NewGuid():N}@example.test",
            password = TestPassword,
            paymentMethod = "bank_transfer",
            currency = "USD",
            items = new[]
            {
                new
                {
                    productId,
                    billingCycle = nameof(BillingCycle.Annual),
                    domain = $"example.{tld}",
                    domainAction = nameof(DomainAction.Register),
                    years = 1,
                },
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var invoiceId = body.RootElement.GetProperty("invoiceId").GetInt32();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId)).SubTotal.Should().Be(usdSellPrice);
    }

    /// <summary>
    /// The response carries the order's real number, the one stored on the order, so a client
    /// never has to build it from the id.
    /// </summary>
    [Fact]
    public async Task PlacedOrderAnswersWithItsStoredOrderNumber()
    {
        await EnableBankTransferAsync();
        var productId = await SeedProductAsync();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/orders", GuestOrder(productId));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var orderId = body.RootElement.GetProperty("orderId").GetInt32();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Orders.AsNoTracking().Where(o => o.Id == orderId).Select(o => o.OrderNumber).SingleAsync();
        body.RootElement.GetProperty("orderNumber").GetString().Should().Be(stored);
    }

    /// <summary>
    /// When Billing cannot raise the invoice, no order is left behind: the order is saved only
    /// once its invoice exists, so a client retrying never meets a Pending order nobody can pay.
    /// </summary>
    /// <remarks>
    /// The failure is a real database refusal: a trigger on the invoice lines table refuses the one
    /// line carrying this test's product name, and is dropped again afterwards.
    /// </remarks>
    [Fact]
    public async Task InvoiceCreationFailureLeavesNoOrder()
    {
        await EnableBankTransferAsync();
        var marker = $"Fail-{Guid.NewGuid():N}";
        var productId = await SeedProductAsync(name: marker);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "CREATE OR REPLACE FUNCTION \"RefuseMarkedInvoiceItem\"() RETURNS trigger AS $$ BEGIN " +
                "IF NEW.\"Description\" LIKE 'Fail-%' THEN RAISE EXCEPTION 'refused by test'; END IF; RETURN NEW; END $$ LANGUAGE plpgsql;");
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER \"RefuseMarkedInvoiceItem\" BEFORE INSERT ON \"InvoiceItems\" " +
                "FOR EACH ROW EXECUTE FUNCTION \"RefuseMarkedInvoiceItem\"();");
        }

        try
        {
            var response = await factory.CreateClient().PostAsJsonAsync("/api/orders", GuestOrder(productId));

            response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS \"RefuseMarkedInvoiceItem\" ON \"InvoiceItems\";");
            await db.Database.ExecuteSqlRawAsync("DROP FUNCTION IF EXISTS \"RefuseMarkedInvoiceItem\"();");
        }

        using var check = factory.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<AppDbContext>();
        (await context.Orders.AsNoTracking().AnyAsync(o => o.Items.Any(i => i.ProductId == productId))).Should().BeFalse();
        (await context.Invoices.AsNoTracking().AnyAsync(i => i.Items.Any(l => l.Description == marker))).Should().BeFalse();
    }

    /// <summary>
    /// A free plan's order owes nothing, so it is settled and fulfilled at placement instead of
    /// waiting for a payment no gateway would take.
    /// </summary>
    [Fact]
    public async Task ZeroTotalOrderIsSettledAndFulfilledAtPlacement()
    {
        await EnableBankTransferAsync();
        var productId = await SeedProductAsync(usdMonthly: 0m);

        var response = await factory.CreateClient().PostAsJsonAsync("/api/orders", GuestOrder(productId));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // A zero total is what tells the checkout there is nothing to pay and no payment step to open.
        body.RootElement.GetProperty("total").GetDecimal().Should().Be(0m);
        var orderId = body.RootElement.GetProperty("orderId").GetInt32();
        var invoiceId = body.RootElement.GetProperty("invoiceId").GetInt32();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId)).Status.Should().Be(InvoiceStatus.Paid);
        (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId)).Status.Should().NotBe(OrderStatus.Pending);
        (await db.Transactions.AsNoTracking().AnyAsync(t => t.InvoiceId == invoiceId)).Should().BeFalse();
    }

    /// <summary>
    /// The invoice line of an order keeps a copy of the product as it was sold: its id, cycle, digital flag, SKU
    /// and metadata, so what a paid line delivers does not follow later catalogue edits.
    /// </summary>
    [Fact]
    public async Task OrderLineKeepsTheProductSnapshotOnItsInvoiceLine()
    {
        await EnableBankTransferAsync();
        var sku = $"credits-{Guid.NewGuid():N}";
        var productId = await SeedProductAsync(type: ProductType.Digital, sku: sku, metadata: "{\"credits\":100}");

        var response = await factory.CreateClient().PostAsJsonAsync("/api/orders", GuestOrder(productId));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var invoiceId = body.RootElement.GetProperty("invoiceId").GetInt32();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invoice = await db.Invoices.AsNoTracking().Include(i => i.Items).SingleAsync(i => i.Id == invoiceId);
        var snapshot = invoice.Items.Should().ContainSingle().Which.ProductSnapshot!;
        snapshot.Should().BeEquivalentTo(
            new ProductSnapshot(productId, BillingCycle.Monthly, IsDigital: true, Sku: sku.ToUpperInvariant(), Metadata: null),
            options => options.Excluding(s => s.Metadata));

        // The database normalises the JSON text (spacing), so the metadata is compared by content, not by characters.
        JsonDocument.Parse(snapshot.Metadata!).RootElement.GetProperty("credits").GetInt32().Should().Be(100);
        invoice.HasDigitalItems.Should().BeTrue();
    }

    /// <summary>
    /// A free digital order is settled and fulfilled at placement: its service is Active at once and was never
    /// provisioned, where a free hosting order leaves its service Pending for the client's setup.
    /// </summary>
    [Fact]
    public async Task FreeDigitalOrderActivatesItsServiceWithoutProvisioning()
    {
        await EnableBankTransferAsync();
        var digitalId = await SeedProductAsync(usdMonthly: 0m, type: ProductType.Digital);
        var hostingId = await SeedProductAsync(usdMonthly: 0m);

        var digital = await factory.CreateClient().PostAsJsonAsync("/api/orders", GuestOrder(digitalId));
        var hosting = await factory.CreateClient().PostAsJsonAsync("/api/orders", GuestOrder(hostingId));

        digital.StatusCode.Should().Be(HttpStatusCode.Created, await digital.Content.ReadAsStringAsync());
        hosting.StatusCode.Should().Be(HttpStatusCode.Created, await hosting.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var digitalService = await db.ClientServices.AsNoTracking().SingleAsync(s => s.ProductId == digitalId);
        digitalService.Status.Should().Be(ServiceStatus.Active);
        digitalService.NextRenewalAt.Should().NotBeNull();
        digitalService.ProvisioningRef.Should().BeNull();
        digitalService.ServerId.Should().BeNull();
        (await db.ClientServices.AsNoTracking().SingleAsync(s => s.ProductId == hostingId)).Status.Should().Be(ServiceStatus.Pending);
    }

    /// <summary>
    /// A renewal of a domain the caller does not hold is refused at checkout, before any account,
    /// number or invoice is created, rather than after the money was taken.
    /// </summary>
    [Fact]
    public async Task RenewalOfADomainNotHeldIsRefusedAtCheckout()
    {
        await EnableBankTransferAsync();
        var tld = $"x{Guid.NewGuid():N}"[..9];
        await SeedUsdTldAndRatesAsync(tld, 10m);
        var productId = await SeedDomainProductAsync();
        var email = $"guest-{Guid.NewGuid():N}@example.test";

        var response = await factory.CreateClient().PostAsJsonAsync("/api/orders", new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            email,
            password = TestPassword,
            paymentMethod = "bank_transfer",
            currency = "USD",
            items = new[]
            {
                new
                {
                    productId,
                    billingCycle = nameof(BillingCycle.Annual),
                    domain = $"someone-else.{tld}",
                    domainAction = nameof(DomainAction.Renew),
                    years = 1,
                },
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertCodeAsync(response, HostpanelErrorCodes.DOMAIN_NOT_FOUND);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Users.AsNoTracking().AnyAsync(u => u.Email == email)).Should().BeFalse();
    }

    /// <summary>
    /// Domain lines are checked by the validator: an unknown action, a missing name, a period
    /// outside 1 to 10 years and a transfer without its code are each a 400.
    /// </summary>
    /// <param name="action">The line's domain action.</param>
    /// <param name="domain">The line's domain name.</param>
    /// <param name="years">The line's period.</param>
    /// <param name="eppCode">The line's authorization code.</param>
    [Theory]
    [InlineData("steal", "example.com", 1, null)]
    [InlineData(nameof(DomainAction.Register), null, 1, null)]
    [InlineData(nameof(DomainAction.Register), "example.com", 11, null)]
    [InlineData(nameof(DomainAction.Transfer), "example.com", 1, null)]
    public async Task InvalidDomainLineIsRefusedByTheValidator(string action, string? domain, int years, string? eppCode)
    {
        await EnableBankTransferAsync();
        var productId = await SeedDomainProductAsync();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/orders", new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            email = $"guest-{Guid.NewGuid():N}@example.test",
            password = TestPassword,
            paymentMethod = "bank_transfer",
            items = new[]
            {
                new { productId, billingCycle = nameof(BillingCycle.Annual), domain, domainAction = action, years, eppCode },
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertCodeAsync(response, "VALIDATION_ERROR");
    }

    /// <summary>A guest checking out with an address that already has an account is answered 409.</summary>
    [Fact]
    public async Task GuestCheckoutWithATakenEmailIsAConflict()
    {
        await EnableBankTransferAsync();
        var productId = await SeedProductAsync();
        var email = $"taken-{Guid.NewGuid():N}@example.test";
        var first = await factory.CreateClient().PostAsJsonAsync("/api/orders", GuestOrder(productId, email));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await factory.CreateClient().PostAsJsonAsync("/api/orders", GuestOrder(productId, email));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await AssertCodeAsync(second, HostpanelErrorCodes.ACCOUNT_EMAIL_EXISTS);
    }

    /// <summary>A signed-in caller's first order keeps the phone number they gave, as a guest's does.</summary>
    [Fact]
    public async Task SignedInCallersFirstOrderStoresTheirPhoneNumber()
    {
        await EnableBankTransferAsync();
        var productId = await SeedProductAsync();

        // Created straight in the identity store, so no registration event links a client row
        // behind the test's back: the order below is always the one that creates it.
        var caller = await ClientsTestSeeds.CreateUnlinkedUserAsync(factory, "first-order");

        var response = await caller.Client.PostAsJsonAsync("/api/orders", new
        {
            phoneNumber = "+37411111111",
            paymentMethod = "bank_transfer",
            items = new[] { new { productId, billingCycle = nameof(BillingCycle.Monthly) } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var orderId = body.RootElement.GetProperty("orderId").GetInt32();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clientId = await db.Orders.AsNoTracking().Where(o => o.Id == orderId).Select(o => o.ClientId).SingleAsync();
        var stored = await db.Clients.AsNoTracking().SingleAsync(c => c.Id == clientId);

        stored.UserId.Should().Be(caller.UserId);
        stored.Phone.Should().Be("+37411111111");
    }

    /// <summary>A complete guest checkout body for one monthly line of a product.</summary>
    /// <param name="productId">The product to order.</param>
    /// <param name="email">The guest's address; a fresh one when omitted.</param>
    /// <returns>The request body.</returns>
    private static object GuestOrder(int productId, string? email = null) => new
    {
        firstName = "Ada",
        lastName = "Lovelace",
        email = email ?? $"guest-{Guid.NewGuid():N}@example.test",
        password = TestPassword,
        paymentMethod = "bank_transfer",
        items = new[] { new { productId, billingCycle = nameof(BillingCycle.Monthly) } },
    };

    /// <summary>Seeds a USD-sold TLD and the AMD and USD rates, so the conversion is deterministic.</summary>
    /// <param name="tld">The extension, without the dot.</param>
    /// <param name="usdSellPrice">The one-year USD sell price of registration.</param>
    /// <returns>The AMD amount the stored rates convert the USD price to.</returns>
    private async Task<decimal> SeedUsdTldAndRatesAsync(string tld, decimal usdSellPrice)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rows = await db.Currencies.ToListAsync();
        var usd = rows.SingleOrDefault(c => c.Code == "USD");
        if (usd is null)
        {
            usd = Currency.Create("USD", "840", "$", string.Empty, 2, 1m, isBase: !rows.Any(c => c.IsBase));
            db.Currencies.Add(usd);
        }

        var amd = rows.SingleOrDefault(c => c.Code == "AMD");
        if (amd is null)
        {
            amd = Currency.Create("AMD", "051", "֏", string.Empty, 2, 0.0026m, isBase: false);
            db.Currencies.Add(amd);
        }

        var prices = new Dictionary<int, decimal> { [1] = usdSellPrice };
        var config = TldConfig.Create(tld, RegistrarModule.NameAm, "USD", "USD", true, 1, ["test"]);
        config.UpdateSellPrices(prices, prices, prices);
        db.TldConfigs.Add(config);
        await db.SaveChangesAsync();

        // The pricing query keeps the enabled configs for minutes; drop them so this TLD is seen.
        scope.ServiceProvider.GetRequiredService<IMemoryCache>().Remove("tld-pricing-configs");

        return usd.ConvertTo(usdSellPrice, amd);
    }

    /// <summary>Seeds an active domain product, the line item a domain order points at.</summary>
    /// <returns>The new product's primary key.</returns>
    private async Task<int> SeedDomainProductAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var group = ProductGroup.Create($"place-order-domain-{Guid.NewGuid():N}", null);
        db.ProductGroups.Add(group);
        await db.SaveChangesAsync();

        var product = Product.Create(
            group.Id, "Domain Registration", null, null, null, null, ProductType.Domain, 0m, 0m, DateTimeOffset.UtcNow);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        return product.Id;
    }

    /// <summary>
    /// Asserts the refusal carries the expected error code, which is what a client branches on.
    /// </summary>
    /// <param name="response">The refusal.</param>
    /// <param name="expected">The code it must carry.</param>
    /// <returns>A task that completes once the body has been read.</returns>
    private static async Task AssertCodeAsync(HttpResponseMessage response, string expected)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(expected);
    }
}
