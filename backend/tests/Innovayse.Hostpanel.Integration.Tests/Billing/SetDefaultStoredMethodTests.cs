using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/me/stored-payment-methods/consents/{id}/default</c> against Postgres: the gateway makes the consent's
/// method the default, and a consent of someone else's, a revoked one, a switched-off module and a failing gateway each
/// answer their own refusal.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SetDefaultStoredMethodTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Builds the route of a consent.</summary>
    /// <param name="consentId">The consent.</param>
    /// <returns>The route.</returns>
    private static string RouteOf(int consentId) => $"/api/me/stored-payment-methods/consents/{consentId}/default";

    /// <summary>The gateway flips the default to the chosen method.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetDefault_OwnConsent_MakesTheMethodTheDefault()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "default-ok");
        await scenario.SeedStoredMethodAsync(signedIn.ClientId);
        var (second, secondRef) = await scenario.SeedStoredMethodAsync(signedIn.ClientId);
        var customerRef = $"demo-cus-client-{signedIn.ClientId}";
        scenario.State.Methods(customerRef).Single(m => m.MethodRef == secondRef).IsDefault.Should().BeFalse();

        var response = await signedIn.Client.PostAsync(RouteOf(second), null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        scenario.State.Methods(customerRef).Single(m => m.IsDefault).MethodRef.Should().Be(secondRef);
    }

    /// <summary>
    /// The host records the choice on the consent and clears it on the client's other consents under the module, so the
    /// stored charge picks the chosen method.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetDefault_OwnConsent_MovesTheHostsDefaultToThatConsent()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "default-host");
        var (first, _) = await scenario.SeedStoredMethodAsync(signedIn.ClientId);
        var (second, _) = await scenario.SeedStoredMethodAsync(signedIn.ClientId);
        (await signedIn.Client.PostAsync(RouteOf(first), null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await signedIn.Client.PostAsync(RouteOf(second), null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await scenario.ConsentAsync(second)).IsDefault.Should().BeTrue();
        (await scenario.ConsentAsync(first)).IsDefault.Should().BeFalse();
    }

    /// <summary>A gateway that refuses leaves the host's default where it was, so the two never disagree.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetDefault_PluginFails_LeavesTheHostsDefaultUntouched()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "default-host-down");
        var (consentId, _) = await scenario.SeedStoredMethodAsync(signedIn.ClientId);
        scenario.Resolver.Register(scenario.Module, new UnreachableStoredCredentialPlugin());

        var response = await signedIn.Client.PostAsync(RouteOf(consentId), null);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await scenario.ConsentAsync(consentId)).IsDefault.Should().BeFalse();
    }

    /// <summary>Another client's consent is not found and its default is untouched.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetDefault_AnotherClientsConsent_IsNotFound()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var owner = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "default-owner");
        var stranger = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "default-stranger");
        await scenario.SeedStoredMethodAsync(owner.ClientId);
        var (second, secondRef) = await scenario.SeedStoredMethodAsync(owner.ClientId);

        var response = await stranger.Client.PostAsync(RouteOf(second), null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CARD_CONSENT_NOT_FOUND);
        scenario.State.Methods($"demo-cus-client-{owner.ClientId}").Single(m => m.MethodRef == secondRef).IsDefault.Should().BeFalse();
    }

    /// <summary>A revoked consent is not found.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetDefault_RevokedConsent_IsNotFound()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "default-revoked");
        var (consentId, _) = await scenario.SeedStoredMethodAsync(signedIn.ClientId);
        await scenario.RevokeConsentAsync(consentId);

        var response = await signedIn.Client.PostAsync(RouteOf(consentId), null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CARD_CONSENT_NOT_FOUND);
    }

    /// <summary>A module that is switched off answers 409.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetDefault_PluginDisabled_IsConflict()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "default-off");
        var (consentId, _) = await scenario.SeedStoredMethodAsync(signedIn.ClientId);
        scenario.Resolver.Unregister(scenario.Module);

        var response = await signedIn.Client.PostAsync(RouteOf(consentId), null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.STORED_CREDENTIALS_NOT_SUPPORTED);
    }

    /// <summary>A gateway that throws answers 503, not 500.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetDefault_PluginFails_IsServiceUnavailable()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "default-down");
        var (consentId, _) = await scenario.SeedStoredMethodAsync(signedIn.ClientId);
        scenario.Resolver.Register(scenario.Module, new UnreachableStoredCredentialPlugin());

        var response = await signedIn.Client.PostAsync(RouteOf(consentId), null);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
    }

    /// <summary>A non-positive id is refused by the validator before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetDefault_ZeroId_IsBadRequest()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "default-zero");

        var response = await signedIn.Client.PostAsync(RouteOf(0), null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
