using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/me/stored-payment-methods/sessions</c> against Postgres, with the stored credential capability served
/// by the demo payment plugin: the consent is recorded before the payer reaches the gateway, the gateway customer is
/// kept once per client and module, and a module without the capability, a missing consent box and a foreign return
/// address are refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class BeginSaveStoredMethodTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Posts the begin request as the caller.</summary>
    /// <param name="caller">The signed-in client.</param>
    /// <param name="module">The gateway module.</param>
    /// <param name="consentAccepted">Whether the consent box is ticked.</param>
    /// <param name="returnUrl">The return address.</param>
    /// <returns>The response.</returns>
    private static Task<HttpResponseMessage> BeginAsync(HttpClient caller, string module, bool consentAccepted, string returnUrl) =>
        caller.PostAsJsonAsync(
            "/api/me/stored-payment-methods/sessions",
            new { gatewayModuleId = module, consentAccepted, returnUrl, consentTextVersion = ConsentTextVersionWire.AccountCard });

    /// <summary>A ticked consent opens a session, records the consent without a method and keeps the gateway customer.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Begin_WithConsent_OpensASessionAndRecordsTheConsent()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "begin-ok");

        var response = await BeginAsync(signedIn.Client, scenario.Module, true, BillingTestSeeds.AllowedReturnUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = body.GetProperty("sessionId").GetString()!;
        body.GetProperty("clientToken").GetString().Should().NotBeNullOrEmpty();
        using var scope = scenario.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var consent = await context.CardChargeConsents.AsNoTracking().SingleAsync(c => c.GatewayModuleId == scenario.Module && c.SessionId == sessionId);
        consent.ClientId.Should().Be(signedIn.ClientId);
        consent.GatewayModuleId.Should().Be(scenario.Module);
        consent.MethodRef.Should().BeNull("the method is bound when the session completes");
        consent.IsActive.Should().BeFalse();
        (await context.GatewayCustomers.AsNoTracking().CountAsync(c => c.ClientId == signedIn.ClientId && c.GatewayModuleId == scenario.Module))
            .Should().Be(1);
    }

    /// <summary>
    /// The consent keeps its evidence: the wording version from the body, and the address and browser of the request
    /// itself, which win over any value the body carries for them.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Begin_WithConsent_KeepsTheEvidenceFromTheRequestNotTheBody()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "begin-evidence");
        signedIn.Client.DefaultRequestHeaders.UserAgent.ParseAdd("EvidenceBrowser/1.0");

        var response = await signedIn.Client.PostAsJsonAsync(
            "/api/me/stored-payment-methods/sessions",
            new
            {
                gatewayModuleId = scenario.Module,
                consentAccepted = true,
                returnUrl = BillingTestSeeds.AllowedReturnUrl,
                consentTextVersion = ConsentTextVersionWire.AccountCard,
                ipAddress = "203.0.113.99",
                userAgent = "Forged/9.9",
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var sessionId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString()!;
        using var scope = scenario.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var consent = await context.CardChargeConsents.AsNoTracking().SingleAsync(c => c.GatewayModuleId == scenario.Module && c.SessionId == sessionId);
        consent.ConsentTextVersion.Should().Be(ConsentTextVersionWire.AccountCard);
        consent.UserAgent.Should().Be("EvidenceBrowser/1.0");
        consent.IpAddress.Should().NotBe("203.0.113.99", "the address is the connection's, never the body's");
    }

    /// <summary>A consent without the wording version it was given under is refused, so every consent carries its evidence.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Begin_WithoutConsentTextVersion_IsRefusedAndWritesNothing()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "begin-noversion");

        var response = await signedIn.Client.PostAsJsonAsync(
            "/api/me/stored-payment-methods/sessions",
            new { gatewayModuleId = scenario.Module, consentAccepted = true, returnUrl = BillingTestSeeds.AllowedReturnUrl });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be("VALIDATION_ERROR");
        using var scope = scenario.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.CardChargeConsents.AsNoTracking().AnyAsync(c => c.ClientId == signedIn.ClientId)).Should().BeFalse();
    }

    /// <summary>A consent wording version the portal never published is refused, so the evidence only ever names words the payer saw.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Begin_WithAnUnpublishedConsentTextVersion_IsRefusedAndWritesNothing()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "begin-unpublished");

        var response = await signedIn.Client.PostAsJsonAsync(
            "/api/me/stored-payment-methods/sessions",
            new
            {
                gatewayModuleId = scenario.Module,
                consentAccepted = true,
                returnUrl = BillingTestSeeds.AllowedReturnUrl,
                consentTextVersion = "2026-11",
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be("VALIDATION_ERROR");
        using var scope = scenario.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.CardChargeConsents.AsNoTracking().AnyAsync(c => c.ClientId == signedIn.ClientId)).Should().BeFalse();
    }

    /// <summary>A second session of the same client reuses the gateway customer instead of creating another.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Begin_Twice_KeepsOneGatewayCustomer()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "begin-twice");

        (await BeginAsync(signedIn.Client, scenario.Module, true, BillingTestSeeds.AllowedReturnUrl)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await BeginAsync(signedIn.Client, scenario.Module, true, BillingTestSeeds.AllowedReturnUrl)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = scenario.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.GatewayCustomers.AsNoTracking().CountAsync(c => c.ClientId == signedIn.ClientId)).Should().Be(1);
        (await context.CardChargeConsents.AsNoTracking().CountAsync(c => c.ClientId == signedIn.ClientId)).Should().Be(2);
    }

    /// <summary>Without the consent box no session is opened and no consent or customer is recorded.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Begin_WithoutConsent_IsRefusedAndWritesNothing()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "begin-noconsent");

        var response = await BeginAsync(signedIn.Client, scenario.Module, false, BillingTestSeeds.AllowedReturnUrl);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be("VALIDATION_ERROR");
        using var scope = scenario.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.CardChargeConsents.AsNoTracking().AnyAsync(c => c.ClientId == signedIn.ClientId)).Should().BeFalse();
        (await context.GatewayCustomers.AsNoTracking().AnyAsync(c => c.ClientId == signedIn.ClientId)).Should().BeFalse();
    }

    /// <summary>A module that is switched off, or has no capability, is refused with 409 and nothing is recorded.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Begin_ModuleWithoutCapability_IsRefused()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "begin-nocap");

        var response = await BeginAsync(signedIn.Client, "not-installed", true, BillingTestSeeds.AllowedReturnUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.STORED_CREDENTIALS_NOT_SUPPORTED);
    }

    /// <summary>A return address on another origin is refused, so the payer is never handed to a foreign site.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Begin_ForeignReturnUrl_IsRefused()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "begin-foreign");

        var response = await BeginAsync(signedIn.Client, scenario.Module, true, "https://evil.example/return");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.RETURN_URL_NOT_ALLOWED);
    }

    /// <summary>A caller who is not signed in is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Begin_Anonymous_IsUnauthorized()
    {
        using var scenario = new StoredCredentialScenario(factory);

        var response = await BeginAsync(scenario.Host.CreateClient(), scenario.Module, true, BillingTestSeeds.AllowedReturnUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
