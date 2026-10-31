using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/me/payment-methods</c> against Postgres, with card payment gateway replaced by
/// <see cref="FakeCardPaymentGateway"/>: the authenticated client's saved cards, and empty for one who
/// has never saved any.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyPaymentMethodsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
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

    /// <summary>
    /// With no card gateway keys (the demo, a bank-transfer-only install) there can be no saved card, so
    /// the list is empty rather than a 500 from the gateway refusing its empty key (#606).
    /// </summary>
    [Fact]
    public async Task GetMine_CardGatewayNotConfigured_ReturnsAnEmptyList()
    {
        var host = BuildHost(new FakeCardPaymentGateway
        {
            SavedPaymentMethods = [new CardPaymentMethodDto("pm_1", SavedPaymentMethodType.Card, "visa", "4242", 12, 2030, true)],
        }, configured: false);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "list-not-configured");
        await BillingTestSeeds.StampCardCustomerAsync(host, clientId, $"cus_{Guid.NewGuid():N}"[..24]);

        var response = await client.GetAsync("/api/me/payment-methods");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<CardPaymentMethodDto>>();
        body.Should().BeEmpty();
    }

    /// <summary>A client who has never saved a payment method sees an empty list.</summary>
    [Fact]
    public async Task GetMine_WithNoSavedMethod_ReturnsAnEmptyList()
    {
        var host = BuildHost(new FakeCardPaymentGateway());
        var (client, _) = await BillingTestSeeds.RegisterClientAsync(factory, host, "no-cards");

        var response = await client.GetAsync("/api/me/payment-methods");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<CardPaymentMethodDto>>();
        body.Should().BeEmpty();
    }

    /// <summary>Only the card the client agreed to have charged automatically is flagged; a card saved some other way is not.</summary>
    [Fact]
    public async Task GetMine_FlagsOnlyTheCardWithAnActiveAgreement()
    {
        var cardGateway = new FakeCardPaymentGateway
        {
            SavedPaymentMethods =
            [
                new CardPaymentMethodDto("pm_agreed", SavedPaymentMethodType.Card, "visa", "4242", 12, 2030, true),
                new CardPaymentMethodDto("pm_plain", SavedPaymentMethodType.Card, "visa", "1881", 11, 2031, false),
            ],
        };
        var host = BuildHost(cardGateway);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "list-auto-charge");
        await BillingTestSeeds.StampCardCustomerAsync(host, clientId, $"cus_{Guid.NewGuid():N}"[..24]);
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var consent = Domain.Billing.CardChargeConsent.Request(
                clientId, $"seti_{Guid.NewGuid():N}", Domain.Billing.Enums.CardConsentSource.AddCard, null, null, DateTimeOffset.UtcNow);
            consent.Activate("pm_agreed", DateTimeOffset.UtcNow);
            context.CardChargeConsents.Add(consent);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var response = await client.GetAsync("/api/me/payment-methods");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        bool AutoChargeOf(string id) => body.EnumerateArray().Single(m => m.GetProperty("id").GetString() == id).GetProperty("autoCharge").GetBoolean();
        AutoChargeOf("pm_agreed").Should().BeTrue();
        AutoChargeOf("pm_plain").Should().BeFalse();
    }

    /// <summary>A client whose card payment gateway customer has a saved card sees it listed.</summary>
    [Fact]
    public async Task GetMine_WithASavedCard_ListsIt()
    {
        var cardGateway = new FakeCardPaymentGateway
        {
            SavedPaymentMethods = [new CardPaymentMethodDto("pm_1", SavedPaymentMethodType.Card, "visa", "4242", 12, 2030, true)],
        };
        var host = BuildHost(cardGateway);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "one-card");
        await BillingTestSeeds.StampCardCustomerAsync(host, clientId, $"cus_{Guid.NewGuid():N}"[..24]);

        var response = await client.GetAsync("/api/me/payment-methods");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Read as the wire carries it: the card type is the enum member's name.
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.EnumerateArray().Should().ContainSingle(m =>
            m.GetProperty("id").GetString() == "pm_1"
            && m.GetProperty("last4").GetString() == "4242"
            && m.GetProperty("type").GetString() == nameof(SavedPaymentMethodType.Card));
    }

    /// <summary>
    /// A card gateway that cannot be reached answers 503 with <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c>
    /// and a <c>Retry-After</c>, never an unhandled 500.
    /// </summary>
    [Fact]
    public async Task GetMine_GatewayUnavailable_Answers503WithRetryAfter()
    {
        var host = BuildHost(new FakeCardPaymentGateway { ListFailure = CardGatewayFailure.Unavailable });
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "list-unavailable");
        await BillingTestSeeds.StampCardCustomerAsync(host, clientId, $"cus_{Guid.NewGuid():N}"[..24]);

        var response = await client.GetAsync("/api/me/payment-methods");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        response.Headers.RetryAfter.Should().NotBeNull();
        response.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(new ServiceUnavailableOptions().RetryAfterSeconds));
    }

    /// <summary>A card gateway that refuses the listing answers 502 with <c>PAYMENT_METHOD_UPSTREAM_FAILED</c>.</summary>
    [Fact]
    public async Task GetMine_GatewayRefuses_Answers502()
    {
        var host = BuildHost(new FakeCardPaymentGateway { ListFailure = CardGatewayFailure.Refused });
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "list-refused");
        await BillingTestSeeds.StampCardCustomerAsync(host, clientId, $"cus_{Guid.NewGuid():N}"[..24]);

        var response = await client.GetAsync("/api/me/payment-methods");

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_METHOD_UPSTREAM_FAILED);
        response.Headers.RetryAfter.Should().BeNull();
    }

    /// <summary>Listing payment methods is not for anonymous callers.</summary>
    [Fact]
    public async Task GetMineRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/me/payment-methods");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>An admin, who is not a client, is refused with 403.</summary>
    [Fact]
    public async Task GetMineRefusesNonClient()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/me/payment-methods");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
