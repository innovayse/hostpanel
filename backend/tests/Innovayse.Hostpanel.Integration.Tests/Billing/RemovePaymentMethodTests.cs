using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>DELETE /api/me/payment-methods/{id}</c> against Postgres, with card payment gateway replaced by
/// <see cref="FakeCardPaymentGateway"/>: detaching a saved card from the authenticated client's own
/// account, and the <c>NO_SAVED_PAYMENT_METHODS</c> / <c>PAYMENT_METHOD_NOT_OWNED</c> refusals.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RemovePaymentMethodTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Builds a host whose card payment gateway calls answer from <paramref name="cardGateway"/>.</summary>
    /// <param name="cardGateway">The fake card payment gateway service the host should report through.</param>
    /// <param name="configured">Whether the gateway has its keys; false stands in for a deployment without them.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(FakeCardPaymentGateway cardGateway, bool configured = true)
    {
        cardGateway.IsConfigured = configured;
        return factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ICardPaymentGateway>(cardGateway)));
    }

    /// <summary>With no card gateway keys there is no card to remove, which is a conflict rather than a 500 (#606).</summary>
    [Fact]
    public async Task Remove_CardGatewayNotConfigured_RefusesPaymentMethodUnavailable()
    {
        var host = BuildHost(new FakeCardPaymentGateway(), configured: false);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "remove-not-configured");
        await BillingTestSeeds.StampCardCustomerAsync(host, clientId, "cus_1");

        var response = await client.DeleteAsync("/api/me/payment-methods/pm_1");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_METHOD_UNAVAILABLE);
    }

    /// <summary>
    /// A client with no card customer still gets <c>NO_SAVED_PAYMENT_METHODS</c> when the gateway has no keys:
    /// that check runs before the gateway check, so the answer does not hide behind a gateway state.
    /// </summary>
    [Fact]
    public async Task Remove_NoCardCustomerAndGatewayNotConfigured_RefusesNoSavedPaymentMethods()
    {
        var cardGateway = new FakeCardPaymentGateway();
        var host = BuildHost(cardGateway, configured: false);
        var (client, _) = await BillingTestSeeds.RegisterClientAsync(factory, host, "remove-none-not-configured");

        var response = await client.DeleteAsync("/api/me/payment-methods/pm_1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.NO_SAVED_PAYMENT_METHODS);
        cardGateway.LastDetachedPaymentMethodId.Should().BeNull();
    }

    /// <summary>Removing a saved card the client owns detaches it from card payment gateway.</summary>
    [Fact]
    public async Task Remove_AnOwnedCard_DetachesIt()
    {
        var cardGateway = new FakeCardPaymentGateway
        {
            SavedPaymentMethods = [new CardPaymentMethodDto("pm_1", SavedPaymentMethodType.Card, "visa", "4242", 12, 2030, true)],
        };
        var host = BuildHost(cardGateway);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "remove-owned");
        await BillingTestSeeds.StampCardCustomerAsync(host, clientId, "cus_1");

        var response = await client.DeleteAsync("/api/me/payment-methods/pm_1");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        cardGateway.LastDetachedPaymentMethodId.Should().Be("pm_1");
    }

    /// <summary>A client who has never saved a card is refused with <c>NO_SAVED_PAYMENT_METHODS</c>.</summary>
    [Fact]
    public async Task Remove_WithNoSavedMethod_RefusesNoSavedPaymentMethods()
    {
        var host = BuildHost(new FakeCardPaymentGateway());
        var (client, _) = await BillingTestSeeds.RegisterClientAsync(factory, host, "remove-none");

        var response = await client.DeleteAsync("/api/me/payment-methods/pm_1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.NO_SAVED_PAYMENT_METHODS);
    }

    /// <summary>An id that does not belong to the caller's card payment gateway customer is refused with <c>PAYMENT_METHOD_NOT_OWNED</c>.</summary>
    [Fact]
    public async Task Remove_AnUnownedId_RefusesPaymentMethodNotOwned()
    {
        var cardGateway = new FakeCardPaymentGateway
        {
            SavedPaymentMethods = [new CardPaymentMethodDto("pm_mine", SavedPaymentMethodType.Card, "visa", "4242", 12, 2030, true)],
        };
        var host = BuildHost(cardGateway);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "remove-unowned");
        await BillingTestSeeds.StampCardCustomerAsync(host, clientId, "cus_1");

        var response = await client.DeleteAsync("/api/me/payment-methods/pm_someone_elses");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_METHOD_NOT_OWNED);
    }

    /// <summary>A card gateway that cannot be reached for the detach answers 503 with <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c>.</summary>
    [Fact]
    public async Task Remove_GatewayUnavailable_Answers503()
    {
        var cardGateway = new FakeCardPaymentGateway
        {
            SavedPaymentMethods = [new CardPaymentMethodDto("pm_1", SavedPaymentMethodType.Card, "visa", "4242", 12, 2030, true)],
            MethodChangeFailure = CardGatewayFailure.Unavailable,
        };
        var host = BuildHost(cardGateway);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "remove-unavailable");
        await BillingTestSeeds.StampCardCustomerAsync(host, clientId, "cus_1");

        var response = await client.DeleteAsync("/api/me/payment-methods/pm_1");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        response.Headers.RetryAfter.Should().NotBeNull();
        cardGateway.LastDetachedPaymentMethodId.Should().BeNull();
    }

    /// <summary>A card gateway that refuses the detach answers 502 with <c>PAYMENT_METHOD_UPSTREAM_FAILED</c>.</summary>
    [Fact]
    public async Task Remove_GatewayRefuses_Answers502()
    {
        var cardGateway = new FakeCardPaymentGateway
        {
            SavedPaymentMethods = [new CardPaymentMethodDto("pm_1", SavedPaymentMethodType.Card, "visa", "4242", 12, 2030, true)],
            MethodChangeFailure = CardGatewayFailure.Refused,
        };
        var host = BuildHost(cardGateway);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "remove-refused");
        await BillingTestSeeds.StampCardCustomerAsync(host, clientId, "cus_1");

        var response = await client.DeleteAsync("/api/me/payment-methods/pm_1");

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_METHOD_UPSTREAM_FAILED);
    }

    /// <summary>Removing a payment method is not for anonymous callers.</summary>
    [Fact]
    public async Task RemoveRefusesAnonymous()
    {
        var response = await factory.CreateClient().DeleteAsync("/api/me/payment-methods/pm_1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
