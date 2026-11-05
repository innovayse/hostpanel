using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/me/stored-payment-methods</c> against Postgres: the caller sees only their own active consents with the
/// method details read live from the gateway, a revoked consent and another client's consent are hidden, and a gateway
/// that fails or is switched off turns into unavailable details instead of an error.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyStoredPaymentMethodsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route under test.</summary>
    private const string Route = "/api/me/stored-payment-methods";

    /// <summary>The caller's own consent is listed with the live brand, last digits, expiry and default flag.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_OwnConsent_ReturnsLiveMethodDetails()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "list-own");
        var (consentId, _) = await scenario.SeedStoredMethodAsync(signedIn.ClientId);
        await scenario.MakeDefaultAsync(consentId);

        var response = await signedIn.Client.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        var item = items.Should().ContainSingle().Subject;
        item.GetProperty("consentId").GetInt32().Should().Be(consentId);
        item.GetProperty("gatewayModuleId").GetString().Should().Be(scenario.Module);
        item.GetProperty("detailsAvailable").GetBoolean().Should().BeTrue();
        item.GetProperty("brand").GetString().Should().Be("visa");
        item.GetProperty("last4").GetString().Should().Be("4242");
        item.GetProperty("expiryMonth").GetInt32().Should().Be(12);
        item.GetProperty("expiryYear").GetInt32().Should().BeGreaterThan(DateTimeOffset.UtcNow.Year);
        item.GetProperty("isDefault").GetBoolean().Should().BeTrue();
    }

    /// <summary>
    /// The default flag is the host's own record: the consent the client chose is the default even though the gateway
    /// still reports its first method as its default, and a consent never chosen is not.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ReportsTheHostsDefaultNotTheGateways()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "list-host-default");
        var (first, _) = await scenario.SeedStoredMethodAsync(signedIn.ClientId);
        var (second, _) = await scenario.SeedStoredMethodAsync(signedIn.ClientId);
        await scenario.MakeDefaultAsync(second);

        var response = await signedIn.Client.GetAsync(Route);

        var items = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        items.Single(i => i.GetProperty("consentId").GetInt32() == second).GetProperty("isDefault").GetBoolean().Should().BeTrue();
        items.Single(i => i.GetProperty("consentId").GetInt32() == first).GetProperty("isDefault").GetBoolean().Should().BeFalse();
    }

    /// <summary>Another client's consent is not in the caller's list, and the caller's own still is.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_AnotherClientsConsent_IsHidden()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var owner = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "list-owner");
        var stranger = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "list-stranger");
        await scenario.SeedStoredMethodAsync(owner.ClientId);
        var (strangerConsent, _) = await scenario.SeedStoredMethodAsync(stranger.ClientId);

        var response = await stranger.Client.GetAsync(Route);

        var items = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        items.Should().ContainSingle().Which.GetProperty("consentId").GetInt32().Should().Be(strangerConsent);
    }

    /// <summary>A revoked consent, and a consent whose save session never completed, are not listed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_RevokedOrMethodlessConsent_IsHidden()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "list-revoked");
        var (revoked, _) = await scenario.SeedStoredMethodAsync(signedIn.ClientId);
        var (active, _) = await scenario.SeedStoredMethodAsync(signedIn.ClientId);
        await scenario.RevokeConsentAsync(revoked);
        await SeedMethodlessConsentAsync(scenario, signedIn.ClientId);

        var response = await signedIn.Client.GetAsync(Route);

        var items = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        items.Should().ContainSingle().Which.GetProperty("consentId").GetInt32().Should().Be(active);
    }

    /// <summary>A gateway that throws does not fail the list: the consent comes back with unavailable details.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_PluginFails_ReturnsConsentWithUnavailableDetails()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "list-down");
        var (consentId, _) = await scenario.SeedStoredMethodAsync(signedIn.ClientId);
        scenario.Resolver.Register(scenario.Module, new UnreachableStoredCredentialPlugin());

        var response = await signedIn.Client.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Should().ContainSingle().Subject;
        AssertUnavailable(item, consentId);
    }

    /// <summary>A gateway module that is switched off does not fail the list either.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_PluginDisabled_ReturnsConsentWithUnavailableDetails()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "list-off");
        var (consentId, _) = await scenario.SeedStoredMethodAsync(signedIn.ClientId);
        scenario.Resolver.Unregister(scenario.Module);

        var response = await signedIn.Client.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Should().ContainSingle().Subject;
        AssertUnavailable(item, consentId);
    }

    /// <summary>A method the gateway no longer holds is listed with unavailable details.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_MethodGoneAtGateway_ReturnsUnavailableDetails()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "list-gone");
        var (consentId, methodRef) = await scenario.SeedStoredMethodAsync(signedIn.ClientId);
        await scenario.Plugin.RemoveMethodAsync($"demo-cus-client-{signedIn.ClientId}", methodRef, CancellationToken.None);

        var response = await signedIn.Client.GetAsync(Route);

        var item = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Should().ContainSingle().Subject;
        AssertUnavailable(item, consentId);
    }

    /// <summary>A client with no consents gets an empty list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_NoConsents_ReturnsEmptyList()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "list-empty");

        var response = await signedIn.Client.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(0);
    }

    /// <summary>Without a session the route answers 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_WithoutSession_IsUnauthorized()
    {
        using var scenario = new StoredCredentialScenario(factory);

        var response = await scenario.Host.CreateClient().GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Asserts a listed entry carries the consent but none of the gateway's details.</summary>
    /// <param name="item">The entry.</param>
    /// <param name="consentId">The consent it must cover.</param>
    private static void AssertUnavailable(JsonElement item, int consentId)
    {
        item.GetProperty("consentId").GetInt32().Should().Be(consentId);
        item.GetProperty("detailsAvailable").GetBoolean().Should().BeFalse();
        item.GetProperty("brand").ValueKind.Should().Be(JsonValueKind.Null);
        item.GetProperty("last4").ValueKind.Should().Be(JsonValueKind.Null);
        item.GetProperty("isDefault").GetBoolean().Should().BeFalse();
    }

    /// <summary>Records a consent whose save session was opened but never completed, so it covers no method yet.</summary>
    /// <param name="scenario">The scenario whose database to change.</param>
    /// <param name="clientId">The client who consented.</param>
    /// <returns>A task that completes once the consent is saved.</returns>
    private static async Task SeedMethodlessConsentAsync(StoredCredentialScenario scenario, int clientId)
    {
        using var scope = scenario.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Innovayse.Hostpanel.Application.Common.Interfaces.IHostpanelDbContext>();
        context.CardChargeConsents.Add(
            CardChargeConsent.Grant(clientId, scenario.Module, $"session-{Guid.NewGuid():N}", DateTimeOffset.UtcNow));
        await context.SaveChangesAsync(CancellationToken.None);
    }
}
