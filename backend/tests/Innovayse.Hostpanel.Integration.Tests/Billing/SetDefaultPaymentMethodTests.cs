using System.Net;
using System.Net.Http.Json;
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
/// <c>PUT /api/me/payment-methods/{id}</c> against Postgres, with card payment gateway replaced by
/// <see cref="FakeCardPaymentGateway"/>: making a saved card the authenticated client's default, and
/// the <c>NO_SAVED_PAYMENT_METHODS</c> / <c>PAYMENT_METHOD_NOT_OWNED</c> refusals.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SetDefaultPaymentMethodTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
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

    /// <summary>With no card gateway keys there is no card to change, which is a conflict rather than a 500 (#606).</summary>
    [Fact]
    public async Task SetDefault_CardGatewayNotConfigured_RefusesPaymentMethodUnavailable()
    {
        var host = BuildHost(new FakeCardPaymentGateway(), configured: false);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "set-default-not-configured");
        await BillingTestSeeds.StampCardCustomerAsync(host, clientId, "cus_1");

        var response = await client.PutAsJsonAsync("/api/me/payment-methods/pm_1", new { setAsDefault = true });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_METHOD_UNAVAILABLE);
    }

    /// <summary>
    /// A client with no card customer still gets <c>NO_SAVED_PAYMENT_METHODS</c> when the gateway has no keys:
    /// that check runs before the gateway check, so the answer does not hide behind a gateway state.
    /// </summary>
    [Fact]
    public async Task SetDefault_NoCardCustomerAndGatewayNotConfigured_RefusesNoSavedPaymentMethods()
    {
        var cardGateway = new FakeCardPaymentGateway();
        var host = BuildHost(cardGateway, configured: false);
        var (client, _) = await BillingTestSeeds.RegisterClientAsync(factory, host, "set-default-none-not-configured");

        var response = await client.PutAsJsonAsync("/api/me/payment-methods/pm_1", new { setAsDefault = true });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.NO_SAVED_PAYMENT_METHODS);
        cardGateway.LastSetDefaultPaymentMethodId.Should().BeNull();
    }

    /// <summary>Setting a saved, owned card as default asks card payment gateway to make it so.</summary>
    [Fact]
    public async Task SetDefault_AnOwnedCard_UpdatesCardGateway()
    {
        var cardGateway = new FakeCardPaymentGateway
        {
            SavedPaymentMethods = [new CardPaymentMethodDto("pm_1", SavedPaymentMethodType.Card, "visa", "4242", 12, 2030, false)],
        };
        var host = BuildHost(cardGateway);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "set-default-owned");
        await BillingTestSeeds.StampCardCustomerAsync(host, clientId, "cus_1");

        var response = await client.PutAsJsonAsync("/api/me/payment-methods/pm_1", new { setAsDefault = true });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        cardGateway.LastSetDefaultPaymentMethodId.Should().Be("pm_1");
    }

    /// <summary>A client who has never saved a card is refused with <c>NO_SAVED_PAYMENT_METHODS</c>.</summary>
    [Fact]
    public async Task SetDefault_WithNoSavedMethod_RefusesNoSavedPaymentMethods()
    {
        var host = BuildHost(new FakeCardPaymentGateway());
        var (client, _) = await BillingTestSeeds.RegisterClientAsync(factory, host, "set-default-none");

        var response = await client.PutAsJsonAsync("/api/me/payment-methods/pm_1", new { setAsDefault = true });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.NO_SAVED_PAYMENT_METHODS);
    }

    /// <summary>An id that does not belong to the caller's card payment gateway customer is refused with <c>PAYMENT_METHOD_NOT_OWNED</c>.</summary>
    [Fact]
    public async Task SetDefault_AnUnownedId_RefusesPaymentMethodNotOwned()
    {
        var cardGateway = new FakeCardPaymentGateway
        {
            SavedPaymentMethods = [new CardPaymentMethodDto("pm_mine", SavedPaymentMethodType.Card, "visa", "4242", 12, 2030, true)],
        };
        var host = BuildHost(cardGateway);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "set-default-unowned");
        await BillingTestSeeds.StampCardCustomerAsync(host, clientId, "cus_1");

        var response = await client.PutAsJsonAsync("/api/me/payment-methods/pm_someone_elses", new { setAsDefault = true });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_METHOD_NOT_OWNED);
    }

    /// <summary>A card gateway that cannot be reached for the change answers 503 with <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c>.</summary>
    [Fact]
    public async Task SetDefault_GatewayUnavailable_Answers503()
    {
        var cardGateway = new FakeCardPaymentGateway
        {
            SavedPaymentMethods = [new CardPaymentMethodDto("pm_1", SavedPaymentMethodType.Card, "visa", "4242", 12, 2030, false)],
            MethodChangeFailure = CardGatewayFailure.Unavailable,
        };
        var host = BuildHost(cardGateway);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "set-default-unavailable");
        await BillingTestSeeds.StampCardCustomerAsync(host, clientId, "cus_1");

        var response = await client.PutAsJsonAsync("/api/me/payment-methods/pm_1", new { setAsDefault = true });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        response.Headers.RetryAfter.Should().NotBeNull();
        cardGateway.LastSetDefaultPaymentMethodId.Should().BeNull();
    }

    /// <summary>A card gateway that refuses to list the methods answers 502 with <c>PAYMENT_METHOD_UPSTREAM_FAILED</c>.</summary>
    [Fact]
    public async Task SetDefault_GatewayRefusesListing_Answers502()
    {
        var cardGateway = new FakeCardPaymentGateway { ListFailure = CardGatewayFailure.Refused };
        var host = BuildHost(cardGateway);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "set-default-refused");
        await BillingTestSeeds.StampCardCustomerAsync(host, clientId, "cus_1");

        var response = await client.PutAsJsonAsync("/api/me/payment-methods/pm_1", new { setAsDefault = true });

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_METHOD_UPSTREAM_FAILED);
        cardGateway.LastSetDefaultPaymentMethodId.Should().BeNull();
    }

    /// <summary>Setting a default payment method is not for anonymous callers.</summary>
    [Fact]
    public async Task SetDefaultRefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync(
            "/api/me/payment-methods/pm_1", new { setAsDefault = true });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
