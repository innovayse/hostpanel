using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <c>POST /api/orders/{id}/gateway-payment/start</c> against Postgres: the shared payment-token
/// guard, the invoiced-order refusal, and the hosted-gateway redirect happy path.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class StartOrderGatewayPaymentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An origin the test host's CORS configuration allows a payer to be returned to.</summary>
    private const string AllowedReturnUrl = "https://client.test/payment/result";

    /// <summary>Builds a host whose payment plugins come from <paramref name="resolver"/>.</summary>
    /// <param name="resolver">The fake resolver the host should resolve plugins through.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(FakePaymentPluginResolver resolver) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentPluginResolver>(resolver)));

    /// <summary>A wrong payment token is refused as <c>ORDER_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WrongTokenRefusesNotFound()
    {
        var host = BuildHost(new FakePaymentPluginResolver());
        var (orderId, _, _) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        var client = host.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/orders/{orderId}/gateway-payment/start",
            new { module = "bank-gateway", returnUrl = AllowedReturnUrl, paymentToken = "wrong" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_NOT_FOUND);
    }

    /// <summary>An order with no linked invoice is refused as <c>ORDER_NOT_INVOICED</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UninvoicedOrderRefusesNotInvoiced()
    {
        var host = BuildHost(new FakePaymentPluginResolver());
        var (orderId, paymentToken) = await OrdersTestSeeds.SeedUninvoicedOrderAsync(factory);
        var client = host.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/orders/{orderId}/gateway-payment/start",
            new { module = "bank-gateway", returnUrl = AllowedReturnUrl, paymentToken });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_NOT_INVOICED);
    }

    /// <summary>Starting a gateway payment with the right token registers a session and returns a redirect URL.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StartWithTheRightTokenReturnsARedirectUrl()
    {
        var plugin = new FakePaymentPlugin { CurrencyCode = "840" };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        var (orderId, paymentToken, _) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        var client = host.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/orders/{orderId}/gateway-payment/start",
            new { module = "bank-gateway", returnUrl = AllowedReturnUrl, paymentToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("redirectUrl").GetString().Should().Be(plugin.CreatePaymentResult.RedirectUrl);
    }

    /// <summary>
    /// A return URL on an origin the panel does not trust, or one that is not a web address, is refused
    /// before any payment session is registered, so a payer is never handed to a foreign site.
    /// </summary>
    /// <param name="returnUrl">The return URL the caller supplies.</param>
    /// <param name="code">The error code the refusal carries.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("https://attacker.example/payment/result", HostpanelErrorCodes.RETURN_URL_NOT_ALLOWED)]
    [InlineData("javascript:alert(1)", "VALIDATION_ERROR")]
    public async Task UntrustedReturnUrlIsRefused(string returnUrl, string code)
    {
        var plugin = new FakePaymentPlugin { CurrencyCode = "840" };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        var (orderId, paymentToken, _) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);

        var response = await host.CreateClient().PostAsJsonAsync(
            $"/api/orders/{orderId}/gateway-payment/start",
            new { module = "bank-gateway", returnUrl, paymentToken });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await OrdersTestSeeds.AssertCodeAsync(response, code);
    }
}
