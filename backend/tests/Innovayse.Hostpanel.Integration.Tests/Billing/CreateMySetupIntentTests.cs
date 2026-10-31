using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/me/payment-methods/setup-intent</c> against Postgres with the card gateway replaced by
/// <see cref="FakeCardPaymentGateway"/>: a client who accepts the agreement gets a setup intent to confirm in
/// the browser, the agreement waits as pending until the gateway confirms the card, and nobody who did not
/// accept (or may not pay by card) gets one.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateMySetupIntentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route under test.</summary>
    private const string Route = "/api/me/payment-methods/setup-intent";

    /// <summary>An accepted agreement opens a setup intent for the client's own customer and records the agreement as pending.</summary>
    [Fact]
    public async Task Create_Accepted_OpensIntentAndRecordsPendingAgreementAsync()
    {
        var gateway = new FakeCardPaymentGateway { IsConfigured = true };
        var host = BuildHost(gateway);
        var (caller, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "setup");

        var response = await caller.PostAsJsonAsync(Route, new { consentAccepted = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("setupIntentId").GetString().Should().Be("seti_test_1");
        body.GetProperty("clientSecret").GetString().Should().Be("seti_test_1_secret");
        gateway.LastSetupInstruction!.ClientId.Should().Be(clientId);
        gateway.LastSetupInstruction.CustomerId.Should().Be(await StoredCustomerAsync(host, clientId));
        var consent = await ConsentsOfAsync(host, clientId);
        consent.Should().ContainSingle();
        consent[0].Status.Should().Be(CardConsentStatus.Pending);
        consent[0].Source.Should().Be(CardConsentSource.AddCard);
        consent[0].GatewayIntentId.Should().Be("seti_test_1");
        consent[0].TextVersion.Should().Be(Domain.Billing.CardChargeConsent.CurrentTextVersion);
    }

    /// <summary>Without the accepted agreement nothing is opened: no customer, no intent, no agreement row.</summary>
    [Fact]
    public async Task Create_NotAccepted_RefusesAndOpensNothingAsync()
    {
        var gateway = new FakeCardPaymentGateway { IsConfigured = true };
        var host = BuildHost(gateway);
        var (caller, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "setup");

        var response = await caller.PostAsJsonAsync(Route, new { consentAccepted = false });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        gateway.LastSetupInstruction.Should().BeNull();
        gateway.CustomersCreated.Should().Be(0);
        (await ConsentsOfAsync(host, clientId)).Should().BeEmpty();
    }

    /// <summary>A deployment without card gateway keys answers <c>PAYMENT_METHOD_UNAVAILABLE</c>.</summary>
    [Fact]
    public async Task Create_GatewayNotConfigured_RefusesUnavailableAsync()
    {
        var host = BuildHost(new FakeCardPaymentGateway { IsConfigured = false });
        var (caller, _) = await BillingTestSeeds.RegisterClientAsync(factory, host, "setup");

        var response = await caller.PostAsJsonAsync(Route, new { consentAccepted = true });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Contain(HostpanelErrorCodes.PAYMENT_METHOD_UNAVAILABLE);
    }

    /// <summary>A client whose card processing is switched off cannot add a card.</summary>
    [Fact]
    public async Task Create_CardProcessingDisabled_RefusesForbiddenAsync()
    {
        var gateway = new FakeCardPaymentGateway { IsConfigured = true };
        var host = BuildHost(gateway);
        var (caller, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "setup");
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var client = await context.Clients.SingleAsync(c => c.Id == clientId);
            client.UpdateSettings(true, false, false, disableCcProcessing: true, false, false, false);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var response = await caller.PostAsJsonAsync(Route, new { consentAccepted = true });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        gateway.LastSetupInstruction.Should().BeNull();
    }

    /// <summary>A caller without a session is refused.</summary>
    [Fact]
    public async Task Create_Anonymous_RefusesUnauthorizedAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(Route, new { consentAccepted = true });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Builds a host whose card gateway is <paramref name="gateway"/>.</summary>
    /// <param name="gateway">The fake card gateway.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(FakeCardPaymentGateway gateway) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ICardPaymentGateway>(gateway)));

    /// <summary>Reads the customer id stored on a client.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="clientId">The client.</param>
    /// <returns>The stored customer id.</returns>
    private static async Task<string?> StoredCustomerAsync(WebApplicationFactory<Program> host, int clientId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Clients.AsNoTracking().Where(c => c.Id == clientId).Select(c => c.CardGatewayCustomerId).SingleAsync();
    }

    /// <summary>Reads a client's card agreements.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="clientId">The client.</param>
    /// <returns>The agreements, oldest first.</returns>
    private static async Task<List<Domain.Billing.CardChargeConsent>> ConsentsOfAsync(WebApplicationFactory<Program> host, int clientId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.CardChargeConsents.AsNoTracking().Where(c => c.ClientId == clientId).OrderBy(c => c.Id).ToListAsync();
    }
}
