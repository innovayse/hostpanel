using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.SDK.Plugins.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// "Start a hosted-gateway payment" (<c>StartGatewayPaymentCommand</c>, reached through
/// <c>POST /api/me/invoices/{id}/gateway-payment/start</c> once <c>StartMyGatewayPaymentHandler</c>
/// settles ownership) against Postgres, with the payment plugin replaced by
/// <see cref="FakePaymentPluginResolver"/>: currency matching, plugin resolution and session state.
/// </summary>
/// <remarks>
/// Every case here goes through the owning client's own route, since that is
/// the only HTTP path to this command; the ownership boundary itself is
/// <see cref="StartMyGatewayPaymentTests"/>'s.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class StartGatewayPaymentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Builds a host whose payment plugins come from <paramref name="resolver"/>.</summary>
    /// <param name="resolver">The fake resolver the host should resolve plugins through.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(FakePaymentPluginResolver resolver) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IPaymentPluginResolver>(resolver)));

    /// <summary>Stamps the client's billing currency, the only way to control what an invoice raised for them bills in.</summary>
    /// <param name="host">The host whose database the client lives in.</param>
    /// <param name="clientId">The client to stamp.</param>
    /// <param name="currency">ISO 4217 alpha code.</param>
    private static async Task SetClientCurrencyAsync(WebApplicationFactory<Program> host, int clientId, string currency)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = await context.Clients.FirstOrDefaultAsync(c => c.Id == clientId, CancellationToken.None);
        client!.SetCurrency(currency);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Starts a gateway payment for <paramref name="invoiceId"/> and returns the raw HTTP response.</summary>
    /// <param name="client">The payer's authenticated client.</param>
    /// <param name="invoiceId">The invoice to start a session for.</param>
    /// <param name="module">The payment module id.</param>
    /// <param name="returnUrl">The return URL to carry.</param>
    /// <returns>The HTTP response.</returns>
    private static Task<HttpResponseMessage> StartAsync(
        HttpClient client, int invoiceId, string module = "bank-gateway", string? returnUrl = null) =>
        client.PostAsJsonAsync(
            $"/api/me/invoices/{invoiceId}/gateway-payment/start",
            new { module, returnUrl = returnUrl ?? BillingTestSeeds.AllowedReturnUrl });

    /// <summary>Registers a payment and stores the session, handing back the gateway's redirect URL.</summary>
    [Fact]
    public async Task StartRegistersPaymentAndStoresSession()
    {
        var plugin = new FakePaymentPlugin { CurrencyCode = "840" };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "gw-start");
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 25.50m);

        var response = await StartAsync(client, invoiceId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("redirectUrl").GetString().Should().Be(plugin.CreatePaymentResult.RedirectUrl);
        plugin.LastCreatePaymentInstruction.Should().NotBeNull();
        plugin.LastCreatePaymentInstruction!.AmountMinor.Should().Be(2550); // 25.50 major -> 2550 minor units
        plugin.LastCreatePaymentInstruction.OrderNumber.Should().StartWith($"INV{invoiceId}-");
        plugin.LastCreatePaymentInstruction.OrderNumber.Length.Should().BeLessOrEqualTo(25);
    }

    /// <summary>An AMD invoice against a plugin configured for AMD registers normally.</summary>
    [Fact]
    public async Task AmdInvoiceAgainstAmdPluginRegistersNormally()
    {
        var plugin = new FakePaymentPlugin { CurrencyCode = "051" };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "gw-start-amd");
        await SetClientCurrencyAsync(host, clientId, "AMD");
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);

        var response = await StartAsync(client, invoiceId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// A USD invoice against a plugin configured for AMD is refused with
    /// <c>GATEWAY_CURRENCY_MISMATCH</c> before the gateway is ever called. The invoice's own
    /// currency decides the match, not the client's current one, and no other lookup is made
    /// that a later currency change on the client record could reach.
    /// </summary>
    [Fact]
    public async Task UsdInvoiceAgainstAmdPluginRefusesWithoutCallingTheGateway()
    {
        var plugin = new FakePaymentPlugin { CurrencyCode = "051" };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "gw-start-usd"); // base currency is USD
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 25m);

        var response = await StartAsync(client, invoiceId);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.GATEWAY_CURRENCY_MISMATCH);
        plugin.CreatePaymentCallCount.Should().Be(0);
    }

    /// <summary>A module the resolver does not know answers <c>PAYMENT_PLUGIN_NOT_CONFIGURED</c>.</summary>
    [Fact]
    public async Task UnavailableModuleRefusesWithPluginNotConfigured()
    {
        var host = BuildHost(new FakePaymentPluginResolver());
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "gw-start-unavailable");
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);

        var response = await StartAsync(client, invoiceId);

        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_PLUGIN_NOT_CONFIGURED);
    }

    /// <summary>An already-paid invoice is refused before the plugin is ever resolved.</summary>
    [Fact]
    public async Task AlreadyPaidInvoiceRefusesWithoutResolvingAPlugin()
    {
        var plugin = new FakePaymentPlugin();
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "gw-start-paid");
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        await BillingTestSeeds.MarkPaidAsync(admin, invoiceId, 10m);

        var response = await StartAsync(client, invoiceId);

        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        plugin.CreatePaymentCallCount.Should().Be(0);
    }

    /// <summary>A return URL whose origin the panel does not allow is refused before any resolution happens.</summary>
    [Fact]
    public async Task ReturnUrlOriginNotAllowedRefuses()
    {
        var host = BuildHost(new FakePaymentPluginResolver());
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "gw-start-badurl");
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);

        var response = await StartAsync(client, invoiceId, returnUrl: "https://evil.example/steal");

        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.RETURN_URL_NOT_ALLOWED);
    }

    /// <summary>
    /// A session started moments ago and still pending is refused with
    /// <c>GATEWAY_SESSION_ALREADY_STARTED</c> rather than letting a second one orphan it at the
    /// gateway.
    /// </summary>
    [Fact]
    public async Task FreshLiveSessionRefusesUntilItIsKnownDeclined()
    {
        var plugin = new FakePaymentPlugin
        {
            CreatePaymentResult = new("gw-first", "https://pg.example/pay?mdOrder=gw-first"),
            StatusResult = new(GatewayPaymentState.Pending, null, "orderStatus:0"),
        };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "gw-start-fresh-pending");
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        (await StartAsync(client, invoiceId)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await StartAsync(client, invoiceId);

        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.GATEWAY_SESSION_ALREADY_STARTED);
        plugin.CreatePaymentCallCount.Should().Be(1);
    }

    /// <summary>A fresh session already known to be declined is replaced with a new one.</summary>
    [Fact]
    public async Task FreshLiveSessionAlreadyDeclinedIsReplaced()
    {
        var plugin = new FakePaymentPlugin
        {
            CreatePaymentResult = new("gw-first", "https://pg.example/pay?mdOrder=gw-first"),
            StatusResult = new(GatewayPaymentState.Declined, null, "orderStatus:6"),
        };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "gw-start-fresh-declined");
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        (await StartAsync(client, invoiceId)).StatusCode.Should().Be(HttpStatusCode.OK);
        plugin.CreatePaymentResult = new("gw-second", "https://pg.example/pay?mdOrder=gw-second");

        var response = await StartAsync(client, invoiceId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("redirectUrl").GetString().Should().Be("https://pg.example/pay?mdOrder=gw-second");
    }

    /// <summary>
    /// A plugin that throws while registering the payment (a gateway outage) answers 503 with
    /// <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> and <c>Retry-After</c>, never a 500; the gateway's
    /// own text stays out of the body and no session is stored on the invoice.
    /// </summary>
    [Fact]
    public async Task PluginOutageAnswersServiceUnavailableWithoutStoringASession()
    {
        const string gatewayText = "bank-gateway internal fault 0xDEAD";
        var plugin = new FakePaymentPlugin
        {
            CurrencyCode = "840",
            CreatePaymentThrows = new HttpRequestException(gatewayText),
        };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "gw-start-outage");
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);

        var response = await StartAsync(client, invoiceId);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        response.Headers.RetryAfter.Should().NotBeNull();
        (await response.Content.ReadAsStringAsync()).Should().NotContain(gatewayText);
        plugin.CreatePaymentCallCount.Should().Be(1);

        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await context.Invoices.AsNoTracking().FirstAsync(i => i.Id == invoiceId, CancellationToken.None);
        invoice.GatewayOrderId.Should().BeNull();
        invoice.GatewayStartedAt.Should().BeNull();
    }

    /// <summary>
    /// After a partial manual payment, the gateway session charges only what is still due, never the
    /// whole total a second time.
    /// </summary>
    [Fact]
    public async Task StartAfterPartialPayment_ChargesOnlyTheRemainder()
    {
        var plugin = new FakePaymentPlugin { CurrencyCode = "840" };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "gw-partial");
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 25.50m);
        (await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/payment", new
        {
            date = DateTimeOffset.UtcNow,
            gateway = "Manual",
            transactionId = $"pay-{Guid.NewGuid():N}",
            amount = 10m,
            fees = 0m,
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await StartAsync(client, invoiceId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        plugin.LastCreatePaymentInstruction!.AmountMinor.Should().Be(1550); // 25.50 - 10.00 due
    }
}
