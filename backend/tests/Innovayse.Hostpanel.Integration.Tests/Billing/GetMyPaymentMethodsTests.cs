using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Settings;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/me/payment-methods</c> against Postgres, with the saved-method capability replaced by
/// <see cref="FakeStoredCredentialPlugin"/>: the authenticated client's saved cards across every module they have an
/// account with, flagged for automatic charging only where an Active agreement exists for that module and card.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyPaymentMethodsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route under test.</summary>
    private const string Route = "/api/me/payment-methods";

    /// <summary>A module nothing serves, standing for a payment plugin that was disabled after the client saved a card with it.</summary>
    private const string OfflineModule = "offline-gateway";

    /// <summary>
    /// A module that is not offered (switched off or not installed) is skipped, so the list is empty rather than a
    /// 500 from a gateway that cannot be asked.
    /// </summary>
    [Fact]
    public async Task GetMine_ModuleNotOffered_ReturnsAnEmptyList()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.Methods.Add(StoredCredentialHost.Card("pm_1"));
        gateway.Resolver.Offered = false;
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "list-not-offered");
        await gateway.LinkAsync(clientId, $"cus_{Guid.NewGuid():N}"[..24]);

        var response = await client.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<List<CardPaymentMethodDto>>()).Should().BeEmpty();
    }

    /// <summary>A client who has never saved a payment method sees an empty list.</summary>
    [Fact]
    public async Task GetMine_WithNoSavedMethod_ReturnsAnEmptyList()
    {
        var gateway = StoredCredentialHost.Create(factory);
        var (client, _) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "no-cards");

        var response = await client.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<List<CardPaymentMethodDto>>()).Should().BeEmpty();
    }

    /// <summary>A client whose gateway customer has a saved card sees it listed with its module and details, and no type member.</summary>
    [Fact]
    public async Task GetMine_WithASavedCard_ListsIt()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.Methods.Add(StoredCredentialHost.Card("pm_1"));
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "one-card");
        await gateway.LinkAsync(clientId, $"cus_{Guid.NewGuid():N}"[..24]);

        var response = await client.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var card = body.EnumerateArray().Should().ContainSingle().Subject;
        card.GetProperty("gatewayModuleId").GetString().Should().Be(StoredCredentialHost.Module);
        card.GetProperty("id").GetString().Should().Be("pm_1");
        card.GetProperty("brand").GetString().Should().Be("visa");
        card.GetProperty("last4").GetString().Should().Be("4242");
        card.GetProperty("expiryMonth").GetInt32().Should().Be(12);
        card.GetProperty("expiryYear").GetInt32().Should().Be(2035);
        card.GetProperty("isDefault").GetBoolean().Should().BeTrue();
        card.TryGetProperty("type", out _).Should().BeFalse();
    }

    /// <summary>
    /// Only the card the client agreed to have charged automatically is flagged, and only at the module the
    /// agreement was made with: a card saved some other way, a Pending or Revoked agreement and an agreement for
    /// the same reference at another module do not flag it.
    /// </summary>
    [Fact]
    public async Task GetMine_FlagsOnlyTheCardWithAnActiveAgreementAtThatModule()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.Methods.AddRange(
        [
            StoredCredentialHost.Card("pm_agreed"),
            StoredCredentialHost.Card("pm_plain", isDefault: false),
            StoredCredentialHost.Card("pm_pending", isDefault: false),
            StoredCredentialHost.Card("pm_elsewhere", isDefault: false),
        ]);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "list-auto-charge");
        await gateway.LinkAsync(clientId, $"cus_{Guid.NewGuid():N}"[..24]);
        await gateway.AddConsentAsync(clientId, "pm_agreed");
        await gateway.AddConsentAsync(clientId, methodRef: null);
        await gateway.AddConsentAsync(clientId, "pm_elsewhere", module: "another-module");

        var response = await client.GetAsync(Route);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        bool AutoChargeOf(string id) => body.EnumerateArray().Single(m => m.GetProperty("id").GetString() == id).GetProperty("autoCharge").GetBoolean();
        AutoChargeOf("pm_agreed").Should().BeTrue();
        AutoChargeOf("pm_plain").Should().BeFalse();
        AutoChargeOf("pm_pending").Should().BeFalse();
        AutoChargeOf("pm_elsewhere").Should().BeFalse();
    }

    /// <summary>The list spans every module the client has an account with, and skips one that is not offered.</summary>
    [Fact]
    public async Task GetMine_ListsAcrossModules_SkippingOneThatIsNotOffered()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.Methods.Add(StoredCredentialHost.Card("pm_1"));
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "list-modules");
        await gateway.LinkAsync(clientId, $"cus_{Guid.NewGuid():N}"[..24]);
        await BillingTestSeeds.LinkGatewayCustomerAsync(gateway.Host, clientId, OfflineModule, $"cus_{Guid.NewGuid():N}"[..24]);

        var response = await client.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var listed = await response.Content.ReadFromJsonAsync<List<CardPaymentMethodDto>>();
        listed.Should().ContainSingle().Which.GatewayModuleId.Should().Be(StoredCredentialHost.Module);
    }

    /// <summary>
    /// A gateway that cannot be reached answers 503 with <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> and a
    /// <c>Retry-After</c>, never an unhandled 500.
    /// </summary>
    [Fact]
    public async Task GetMine_GatewayUnavailable_Answers503WithRetryAfter()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.MethodCallsThrow = true;
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "list-unavailable");
        await gateway.LinkAsync(clientId, $"cus_{Guid.NewGuid():N}"[..24]);

        var response = await client.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        response.Headers.RetryAfter.Should().NotBeNull();
        response.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(new ServiceUnavailableOptions().RetryAfterSeconds));
    }

    /// <summary>Listing payment methods is not for anonymous callers.</summary>
    [Fact]
    public async Task GetMineRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>An admin, who is not a client, is refused with 403.</summary>
    [Fact]
    public async Task GetMineRefusesNonClient()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
