using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Payment;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// What the start of a hosted-gateway payment tells the portal for each kind of session a plugin can register: the redirect,
/// the form post it renders as an auto submitted form and the browser step it waits for the return of. A session the host
/// cannot honour (a form that breaks the contract's rules, a browser step from a plugin that cannot complete a return) is a
/// failed payment: no session is stored and the order the gateway just registered is voided.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class StartGatewayPaymentSessionKindTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The module every test registers its fake under.</summary>
    private const string Module = "bank-gateway";

    /// <summary>Builds a host whose payment plugins come from <paramref name="resolver"/>.</summary>
    /// <param name="resolver">The fake resolver the host should resolve plugins through.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(FakePaymentPluginResolver resolver)
    {
        return factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentPluginResolver>(resolver)));
    }

    /// <summary>Starts a gateway payment on a fresh invoice of a fresh client, with the plugin registered under the module.</summary>
    /// <param name="plugin">The plugin the module resolves to.</param>
    /// <param name="label">Distinguishes the client.</param>
    /// <returns>The host, the invoice and the response of the start.</returns>
    private async Task<(WebApplicationFactory<Program> Host, int InvoiceId, HttpResponseMessage Response)> StartAsync(IPaymentPlugin plugin, string label)
    {
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(Module, plugin);
        var host = BuildHost(resolver);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, label);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);

        var response = await client.PostAsJsonAsync(
            $"/api/me/invoices/{invoiceId}/gateway-payment/start",
            new { module = Module, returnUrl = BillingTestSeeds.AllowedReturnUrl });
        return (host, invoiceId, response);
    }

    /// <summary>Reads the gateway order the invoice points at.</summary>
    /// <param name="host">The host whose database holds the invoice.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The gateway order id, or <see langword="null"/> when the invoice holds no session.</returns>
    private static async Task<string?> SessionOfAsync(WebApplicationFactory<Program> host, int invoiceId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await context.Invoices.AsNoTracking().FirstAsync(i => i.Id == invoiceId, CancellationToken.None);
        return invoice.GatewayOrderId;
    }

    /// <summary>A plain redirect is answered as a redirect to the plugin's address, with no form.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ARedirect_IsAnsweredAsARedirectWithNoForm()
    {
        var plugin = new FakePaymentPlugin();

        var (_, _, response) = await StartAsync(plugin, "kind-redirect");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("kind").GetString().Should().Be(nameof(GatewaySessionKind.Redirect));
        body.GetProperty("redirectUrl").GetString().Should().Be(plugin.CreatePaymentResult.RedirectUrl);
        body.GetProperty("formPost").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>A form post is answered with its form, so the portal renders an auto submitted form, and the session is stored.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AFormPost_IsAnsweredWithItsForm()
    {
        var form = new PaymentFormPost("https://pg.example/hosted", [new("token", "t1"), new("token", "t2"), new("lang", "hy")]);
        var plugin = new FakePaymentPlugin { CreatePaymentResult = new PaymentSession("gw-form", form.Action, form) };

        var (host, invoiceId, response) = await StartAsync(plugin, "kind-form");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("kind").GetString().Should().Be(nameof(GatewaySessionKind.FormPost));
        body.GetProperty("redirectUrl").GetString().Should().Be(form.Action);
        var sent = body.GetProperty("formPost");
        sent.GetProperty("action").GetString().Should().Be(form.Action);
        sent.GetProperty("fields").EnumerateArray()
            .Select(f => (f.GetProperty("name").GetString(), f.GetProperty("value").GetString()))
            .Should().Equal(("token", "t1"), ("token", "t2"), ("lang", "hy"));
        (await SessionOfAsync(host, invoiceId)).Should().Be("gw-form");
    }

    /// <summary>A browser step from a plugin that completes a return is answered as a browser step to the page of the step.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ABrowserStep_IsAnsweredAsABrowserStepToThePageOfTheStep()
    {
        var plugin = new FakeReturnPaymentPlugin();

        var (host, invoiceId, response) = await StartAsync(plugin, "kind-step");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("kind").GetString().Should().Be(nameof(GatewaySessionKind.BrowserStep));
        body.GetProperty("redirectUrl").GetString().Should().Be("https://pg.example/step");
        body.GetProperty("formPost").ValueKind.Should().Be(JsonValueKind.Null);
        (await SessionOfAsync(host, invoiceId)).Should().Be("gw-step");
        plugin.Calls.Should().BeEmpty("the host waits for the return request and reads no status at the start");
    }

    /// <summary>A browser step from a plugin that cannot complete a return is a failed payment: nothing is stored.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ABrowserStep_FromAPluginThatCannotCompleteAReturn_IsAFailedPayment()
    {
        var plugin = new FakePaymentPlugin
        {
            CreatePaymentResult = new PaymentSession("gw-step", "https://pg.example/step", null, PaymentSessionKind.BrowserStep),
        };

        var (host, invoiceId, response) = await StartAsync(plugin, "kind-step-refused");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        (await SessionOfAsync(host, invoiceId)).Should().BeNull();
    }

    /// <summary>A form post that breaks the contract's rules is a failed payment, and the order the gateway registered is voided.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AFormPost_ThatBreaksTheRules_IsAFailedPaymentAndItsOrderIsCancelled()
    {
        var form = new PaymentFormPost("http://pg.example/hosted", [new("token", "t1")]);
        var plugin = new FakeReturnPaymentPlugin { CreatePaymentResult = new PaymentSession("gw-bad-form", form.Action, form) };

        var (host, invoiceId, response) = await StartAsync(plugin, "kind-form-refused");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        (await SessionOfAsync(host, invoiceId)).Should().BeNull();
        plugin.CancelledOrders.Should().Equal("gw-bad-form");
    }
}
