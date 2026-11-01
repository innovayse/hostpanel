using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.SDK.Plugins.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/me/invoices/{id}/gateway-payment/complete</c> against Postgres: the
/// caller-ownership check <c>CompleteMyGatewayPaymentHandler</c> runs before dispatching to the
/// shared <c>CompleteGatewayPaymentCommand</c> (its own gateway-state rules are covered in
/// <see cref="CompleteGatewayPaymentTests"/>, which this handler dispatches to unchanged).
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CompleteMyGatewayPaymentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Builds a host whose payment plugins come from <paramref name="resolver"/>.</summary>
    /// <param name="resolver">The fake resolver the host should resolve plugins through.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(FakePaymentPluginResolver resolver) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IPaymentPluginResolver>(resolver)));

    /// <summary>The owner completes a gateway payment on their own invoice.</summary>
    [Fact]
    public async Task Complete_TheOwnersInvoice_Returns200()
    {
        var plugin = new FakePaymentPlugin { StatusResult = new(GatewayPaymentState.Paid, "ref-1", "orderStatus:2", AmountMinor: 1000) };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        var (owner, ownerClientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "owner-gw-complete");
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, ownerClientId, 10m);
        plugin.CreatePaymentResult = new("gw-1", "https://pg.example/pay?mdOrder=gw-1");
        (await owner.PostAsJsonAsync(
            $"/api/me/invoices/{invoiceId}/gateway-payment/start",
            new { module = "bank-gateway", returnUrl = BillingTestSeeds.AllowedReturnUrl }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await owner.PostAsync($"/api/me/invoices/{invoiceId}/gateway-payment/complete", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("state").GetString().Should().Be(nameof(GatewayCompletionState.Paid));
    }

    /// <summary>
    /// Completing a hosted-gateway payment on a stranger's invoice is refused with
    /// <c>INVOICE_NOT_FOUND</c> before any payment plugin is resolved.
    /// </summary>
    [Fact]
    public async Task Complete_AStrangersInvoice_Returns404WithInvoiceNotFound()
    {
        var (_, ownerClientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "owner-strangers-complete");
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, ownerClientId, 10m);
        var stranger = await BillingTestSeeds.RegisterStrangerAsync(factory, null, "stranger-complete");

        var response = await stranger.PostAsync($"/api/me/invoices/{invoiceId}/gateway-payment/complete", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>Completing a session is not for anonymous callers.</summary>
    [Fact]
    public async Task CompleteRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsync("/api/me/invoices/1/gateway-payment/complete", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c> before the handler runs.</summary>
    [Fact]
    public async Task CompleteGateway_WithNonPositiveId_Returns400ValidationErrorAsync()
    {
        var (client, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "completemygatewaypayment-validation");

        var response = await client.PostAsync("/api/me/invoices/0/gateway-payment/complete", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
