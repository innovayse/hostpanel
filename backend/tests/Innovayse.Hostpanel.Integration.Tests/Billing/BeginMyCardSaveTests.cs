using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.StoredCredentials;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/me/payment-methods/sessions</c> against Postgres with the saved-method capability replaced by
/// <see cref="FakeStoredCredentialPlugin"/>: a client who accepts the agreement gets a neutral session to continue
/// in the browser, the agreement waits as Pending until the gateway confirms the card, and nobody who did not
/// accept, may not pay by card, or was handed an unusable session gets an agreement row.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class BeginMyCardSaveTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route under test.</summary>
    private const string Route = "/api/me/payment-methods/sessions";

    /// <summary>The page the payer comes back to.</summary>
    private const string ReturnUrl = "https://panel.example.test/client/payment-methods";

    /// <summary>Builds the request body for the fake module.</summary>
    /// <param name="consentAccepted">Whether the payer ticked the agreement.</param>
    /// <returns>The body.</returns>
    private static object Body(bool consentAccepted = true) =>
        new { gatewayModuleId = StoredCredentialHost.Module, returnUrl = ReturnUrl, consentAccepted };

    /// <summary>An accepted agreement opens a session for the client's own account and records a Pending agreement with module and session.</summary>
    [Fact]
    public async Task Begin_Accepted_OpensSessionAndRecordsPendingAgreementAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        var (caller, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "begin");

        var response = await caller.PostAsJsonAsync(Route, Body());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("gatewayModuleId").GetString().Should().Be(StoredCredentialHost.Module);
        body.GetProperty("sessionId").GetString().Should().Be("seti_test_1");
        body.GetProperty("clientToken").GetString().Should().Be("seti_test_1_secret");
        var account = (await gateway.CustomersOfAsync(clientId)).Should().ContainSingle().Subject;
        gateway.Plugin.LastSaveContext.Should().Be(new SaveMethodContext(account.CustomerRef, ReturnUrl));
        var consent = (await gateway.ConsentsOfAsync(clientId)).Should().ContainSingle().Subject;
        consent.Status.Should().Be(CardConsentStatus.Pending);
        consent.GatewayModuleId.Should().Be(StoredCredentialHost.Module);
        consent.GatewaySessionId.Should().Be("seti_test_1");
        consent.GatewayMethodRef.Should().BeNull();
        consent.TextVersion.Should().Be(Domain.Billing.CardChargeConsent.CurrentTextVersion);
    }

    /// <summary>A redirect session reaches the browser with its address.</summary>
    [Fact]
    public async Task Begin_RedirectSession_ReturnsTheAddressAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.BegunSession = new SaveMethodSession("sess_redirect_1", SaveMethodMode.Redirect, null, "https://bank.example.test/card/sess_redirect_1");
        var (caller, _) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "begin-redirect");

        var response = await caller.PostAsJsonAsync(Route, Body());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("redirectUrl").GetString().Should().Be("https://bank.example.test/card/sess_redirect_1");
    }

    /// <summary>Without the accepted agreement nothing is opened: no account, no session, no agreement row.</summary>
    [Fact]
    public async Task Begin_NotAccepted_RefusesAndOpensNothingAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        var (caller, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "begin-declined");

        var response = await caller.PostAsJsonAsync(Route, Body(consentAccepted: false));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        gateway.Plugin.LastSaveContext.Should().BeNull();
        gateway.Plugin.CustomersCreated.Should().Be(0);
        (await gateway.ConsentsOfAsync(clientId)).Should().BeEmpty();
    }

    /// <summary>A session the host cannot hand to a browser is refused as an upstream failure and leaves no agreement row.</summary>
    /// <param name="sessionId">The session id the plugin answers.</param>
    /// <param name="mode">The mode the plugin answers.</param>
    /// <param name="token">The client token the plugin answers.</param>
    /// <param name="redirect">The redirect address the plugin answers.</param>
    [Theory]
    [InlineData("", SaveMethodMode.EmbeddedToken, "tok", null)]
    [InlineData("sess_1", (SaveMethodMode)0, "tok", null)]
    [InlineData("sess_1", SaveMethodMode.EmbeddedToken, null, null)]
    [InlineData("sess_1", SaveMethodMode.Redirect, null, "/relative/only")]
    [InlineData("sess_1", SaveMethodMode.Redirect, null, null)]
    public async Task Begin_UnusableSession_RefusesUpstreamFailedAndRecordsNothingAsync(
        string sessionId, SaveMethodMode mode, string? token, string? redirect)
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.BegunSession = new SaveMethodSession(sessionId, mode, token, redirect);
        var (caller, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "begin-unusable");

        var response = await caller.PostAsJsonAsync(Route, Body());

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_METHOD_UPSTREAM_FAILED);
        (await gateway.ConsentsOfAsync(clientId)).Should().BeEmpty();
    }

    /// <summary>A module that is not offered answers <c>PAYMENT_METHOD_UNAVAILABLE</c> and records nothing.</summary>
    [Fact]
    public async Task Begin_ModuleNotOffered_RefusesUnavailableAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Resolver.Offered = false;
        var (caller, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "begin-not-offered");

        var response = await caller.PostAsJsonAsync(Route, Body());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Contain(HostpanelErrorCodes.PAYMENT_METHOD_UNAVAILABLE);
        (await gateway.ConsentsOfAsync(clientId)).Should().BeEmpty();
    }

    /// <summary>A gateway that cannot be reached answers 503 and records nothing.</summary>
    [Fact]
    public async Task Begin_GatewayUnavailable_Answers503AndRecordsNothingAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.CustomerThrows = true;
        var (caller, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "begin-unavailable");

        var response = await caller.PostAsJsonAsync(Route, Body());

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await gateway.ConsentsOfAsync(clientId)).Should().BeEmpty();
    }

    /// <summary>A client whose card processing is switched off cannot add a card.</summary>
    [Fact]
    public async Task Begin_CardProcessingDisabled_RefusesForbiddenAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        var (caller, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "begin-cc-disabled");
        await ClientSettingsSeeds.SetAsync(factory, clientId, disableCcProcessing: true);

        var response = await caller.PostAsJsonAsync(Route, Body());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_CARD_PAYMENT_DISABLED);
        gateway.Plugin.LastSaveContext.Should().BeNull();
        (await gateway.ConsentsOfAsync(clientId)).Should().BeEmpty();
    }

    /// <summary>A suspended account cannot add a card.</summary>
    [Fact]
    public async Task Begin_SuspendedClient_RefusesForbiddenAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        var (caller, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "begin-suspended");
        await ClientStatusSeeds.SetAsync(factory, clientId, ClientStatus.Suspended);

        var response = await caller.PostAsJsonAsync(Route, Body());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_SUSPENDED);
        (await gateway.ConsentsOfAsync(clientId)).Should().BeEmpty();
    }

    /// <summary>A caller without a session is refused.</summary>
    [Fact]
    public async Task Begin_Anonymous_RefusesUnauthorizedAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(Route, Body());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
