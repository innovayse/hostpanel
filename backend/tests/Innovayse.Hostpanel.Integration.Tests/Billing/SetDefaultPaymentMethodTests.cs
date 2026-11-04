using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>PUT /api/me/payment-methods/{module}/{id}</c> against Postgres, with the saved-method capability replaced by
/// <see cref="FakeStoredCredentialPlugin"/>: making a saved card the authenticated client's default, and the
/// <c>NO_SAVED_PAYMENT_METHODS</c> / <c>PAYMENT_METHOD_NOT_OWNED</c> refusals.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SetDefaultPaymentMethodTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route of a card at the fake module.</summary>
    /// <param name="methodRef">The card.</param>
    /// <returns>The route.</returns>
    private static string RouteOf(string methodRef) => $"/api/me/payment-methods/{StoredCredentialHost.Module}/{methodRef}";

    /// <summary>With the module not offered there is no card to change, which is a conflict rather than a 500.</summary>
    [Fact]
    public async Task SetDefault_ModuleNotOffered_RefusesPaymentMethodUnavailable()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Resolver.Offered = false;
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "set-default-not-offered");
        await gateway.LinkAsync(clientId, $"cus_{Guid.NewGuid():N}"[..24]);

        var response = await client.PutAsJsonAsync(RouteOf("pm_1"), new { setAsDefault = true });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_METHOD_UNAVAILABLE);
    }

    /// <summary>
    /// A client with no account at the module still gets <c>NO_SAVED_PAYMENT_METHODS</c> when the module is not
    /// offered: that check runs before the gateway check, so the answer does not hide behind a gateway state.
    /// </summary>
    [Fact]
    public async Task SetDefault_NoAccountAndModuleNotOffered_RefusesNoSavedPaymentMethods()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Resolver.Offered = false;
        var (client, _) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "set-default-none-not-offered");

        var response = await client.PutAsJsonAsync(RouteOf("pm_1"), new { setAsDefault = true });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.NO_SAVED_PAYMENT_METHODS);
        gateway.Plugin.LastDefaultMethod.Should().BeNull();
    }

    /// <summary>Setting a saved, owned card as default asks the gateway to make it so.</summary>
    [Fact]
    public async Task SetDefault_AnOwnedCard_UpdatesTheGateway()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.Methods.Add(StoredCredentialHost.Card("pm_1", isDefault: false));
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "set-default-owned");
        await gateway.LinkAsync(clientId, $"cus_{Guid.NewGuid():N}"[..24]);

        var response = await client.PutAsJsonAsync(RouteOf("pm_1"), new { setAsDefault = true });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        gateway.Plugin.LastDefaultMethod.Should().Be("pm_1");
    }

    /// <summary>A client who has never saved a card is refused with <c>NO_SAVED_PAYMENT_METHODS</c>.</summary>
    [Fact]
    public async Task SetDefault_WithNoSavedMethod_RefusesNoSavedPaymentMethods()
    {
        var gateway = StoredCredentialHost.Create(factory);
        var (client, _) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "set-default-none");

        var response = await client.PutAsJsonAsync(RouteOf("pm_1"), new { setAsDefault = true });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.NO_SAVED_PAYMENT_METHODS);
    }

    /// <summary>An id that does not belong to the caller's gateway customer is refused with <c>PAYMENT_METHOD_NOT_OWNED</c>.</summary>
    [Fact]
    public async Task SetDefault_AnUnownedId_RefusesPaymentMethodNotOwned()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.Methods.Add(StoredCredentialHost.Card("pm_mine"));
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "set-default-unowned");
        await gateway.LinkAsync(clientId, $"cus_{Guid.NewGuid():N}"[..24]);

        var response = await client.PutAsJsonAsync(RouteOf("pm_someone_elses"), new { setAsDefault = true });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_METHOD_NOT_OWNED);
        gateway.Plugin.LastDefaultMethod.Should().BeNull();
    }

    /// <summary>A gateway that cannot be reached answers 503 with <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> and a <c>Retry-After</c>.</summary>
    [Fact]
    public async Task SetDefault_GatewayUnavailable_Answers503()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.Methods.Add(StoredCredentialHost.Card("pm_1", isDefault: false));
        gateway.Plugin.MethodCallsThrow = true;
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "set-default-unavailable");
        await gateway.LinkAsync(clientId, $"cus_{Guid.NewGuid():N}"[..24]);

        var response = await client.PutAsJsonAsync(RouteOf("pm_1"), new { setAsDefault = true });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        response.Headers.RetryAfter.Should().NotBeNull();
        gateway.Plugin.LastDefaultMethod.Should().BeNull();
    }

    /// <summary>Setting a default payment method is not for anonymous callers.</summary>
    [Fact]
    public async Task SetDefaultRefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync(RouteOf("pm_1"), new { setAsDefault = true });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
