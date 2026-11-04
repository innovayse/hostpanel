using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>DELETE /api/me/payment-methods/{module}/{id}</c> against Postgres, with the saved-method capability replaced
/// by <see cref="FakeStoredCredentialPlugin"/>: detaching a saved card from the authenticated client's own
/// account, the <c>NO_SAVED_PAYMENT_METHODS</c> / <c>PAYMENT_METHOD_NOT_OWNED</c> refusals, and that removing a card
/// ends exactly the automatic-charge agreement made for that card at that module.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RemovePaymentMethodTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route of a card at the fake module.</summary>
    /// <param name="methodRef">The card.</param>
    /// <returns>The route.</returns>
    private static string RouteOf(string methodRef) => $"/api/me/payment-methods/{StoredCredentialHost.Module}/{methodRef}";

    /// <summary>With the module not offered (switched off or not installed) there is no card to remove, which is a conflict rather than a 500.</summary>
    [Fact]
    public async Task Remove_ModuleNotOffered_RefusesPaymentMethodUnavailable()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Resolver.Offered = false;
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "remove-not-offered");
        await gateway.LinkAsync(clientId, $"cus_{Guid.NewGuid():N}"[..24]);

        var response = await client.DeleteAsync(RouteOf("pm_1"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_METHOD_UNAVAILABLE);
    }

    /// <summary>
    /// A client with no account at the module still gets <c>NO_SAVED_PAYMENT_METHODS</c> when the module is not
    /// offered: that check runs before the gateway check, so the answer does not hide behind a gateway state.
    /// </summary>
    [Fact]
    public async Task Remove_NoAccountAndModuleNotOffered_RefusesNoSavedPaymentMethods()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Resolver.Offered = false;
        var (client, _) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "remove-none-not-offered");

        var response = await client.DeleteAsync(RouteOf("pm_1"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.NO_SAVED_PAYMENT_METHODS);
        gateway.Plugin.LastRemovedMethod.Should().BeNull();
    }

    /// <summary>Removing a saved card the client owns detaches it at the gateway.</summary>
    [Fact]
    public async Task Remove_AnOwnedCard_DetachesIt()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.Methods.Add(StoredCredentialHost.Card("pm_1"));
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "remove-owned");
        await gateway.LinkAsync(clientId, $"cus_{Guid.NewGuid():N}"[..24]);

        var response = await client.DeleteAsync(RouteOf("pm_1"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        gateway.Plugin.LastRemovedMethod.Should().Be("pm_1");
    }

    /// <summary>A client who has never saved a card is refused with <c>NO_SAVED_PAYMENT_METHODS</c>.</summary>
    [Fact]
    public async Task Remove_WithNoSavedMethod_RefusesNoSavedPaymentMethods()
    {
        var gateway = StoredCredentialHost.Create(factory);
        var (client, _) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "remove-none");

        var response = await client.DeleteAsync(RouteOf("pm_1"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.NO_SAVED_PAYMENT_METHODS);
    }

    /// <summary>An id that does not belong to the caller's gateway customer is refused with <c>PAYMENT_METHOD_NOT_OWNED</c>.</summary>
    [Fact]
    public async Task Remove_AnUnownedId_RefusesPaymentMethodNotOwned()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.Methods.Add(StoredCredentialHost.Card("pm_mine"));
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "remove-unowned");
        await gateway.LinkAsync(clientId, $"cus_{Guid.NewGuid():N}"[..24]);

        var response = await client.DeleteAsync(RouteOf("pm_someone_elses"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_METHOD_NOT_OWNED);
        gateway.Plugin.LastRemovedMethod.Should().BeNull();
    }

    /// <summary>A gateway that cannot be reached answers 503 with <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c>, a <c>Retry-After</c>, and keeps the agreement.</summary>
    [Fact]
    public async Task Remove_GatewayUnavailable_Answers503AndKeepsTheAgreement()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.Methods.Add(StoredCredentialHost.Card("pm_1"));
        gateway.Plugin.MethodCallsThrow = true;
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "remove-unavailable");
        await gateway.LinkAsync(clientId, $"cus_{Guid.NewGuid():N}"[..24]);
        await gateway.AddConsentAsync(clientId, "pm_1");

        var response = await client.DeleteAsync(RouteOf("pm_1"));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        response.Headers.RetryAfter.Should().NotBeNull();
        gateway.Plugin.LastRemovedMethod.Should().BeNull();
        (await gateway.ConsentsOfAsync(clientId)).Should().ContainSingle().Which.Status.Should().Be(CardConsentStatus.Active);
    }

    /// <summary>
    /// Removing a card ends only the agreement made for that module and that card: the client's agreement for
    /// another card, another client's agreement for the same card, and an agreement for the same method reference
    /// at another module all stay Active.
    /// </summary>
    [Fact]
    public async Task Remove_AnAgreedCard_EndsOnlyThatCardsAgreementAtThatModule()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.Methods.AddRange([StoredCredentialHost.Card("pm_gone"), StoredCredentialHost.Card("pm_kept", isDefault: false)]);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "remove-agreement");
        var (_, otherClientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "remove-agreement-other");
        await gateway.LinkAsync(clientId, $"cus_{Guid.NewGuid():N}"[..24]);
        await gateway.AddConsentAsync(clientId, "pm_gone");
        await gateway.AddConsentAsync(clientId, "pm_kept");
        await gateway.AddConsentAsync(clientId, "pm_gone", module: "another-module");
        await gateway.AddConsentAsync(otherClientId, "pm_gone");

        var response = await client.DeleteAsync(RouteOf("pm_gone"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var own = await gateway.ConsentsOfAsync(clientId);
        var ended = own.Single(c => c.GatewayModuleId == StoredCredentialHost.Module && c.GatewayMethodRef == "pm_gone");
        ended.Status.Should().Be(CardConsentStatus.Revoked);
        ended.RevocationReason.Should().Be(CardConsentRevocationReason.CardRemoved);
        own.Single(c => c.GatewayMethodRef == "pm_kept").Status.Should().Be(CardConsentStatus.Active);
        own.Single(c => c.GatewayModuleId == "another-module").Status.Should().Be(CardConsentStatus.Active);
        (await gateway.ConsentsOfAsync(otherClientId)).Should().ContainSingle().Which.Status.Should().Be(CardConsentStatus.Active);
    }

    /// <summary>Removing a payment method is not for anonymous callers.</summary>
    [Fact]
    public async Task RemoveRefusesAnonymous()
    {
        var response = await factory.CreateClient().DeleteAsync(RouteOf("pm_1"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
