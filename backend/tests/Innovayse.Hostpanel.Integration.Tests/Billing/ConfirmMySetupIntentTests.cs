using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/me/payment-methods/confirm-setup</c> against Postgres with the card gateway replaced by
/// <see cref="FakeCardPaymentGateway"/>: the gateway's own record of the setup intent decides whether the card
/// was saved, and only then does the agreement become active, never because the browser said so.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ConfirmMySetupIntentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The setup route.</summary>
    private const string SetupRoute = "/api/me/payment-methods/setup-intent";

    /// <summary>The confirm route.</summary>
    private const string ConfirmRoute = "/api/me/payment-methods/confirm-setup";

    /// <summary>A setup the gateway reports as saved activates the agreement for that card and makes the first card the default.</summary>
    [Fact]
    public async Task Confirm_SavedAtGateway_ActivatesAgreementAndSetsDefaultAsync()
    {
        var gateway = new FakeCardPaymentGateway { IsConfigured = true };
        var host = BuildHost(gateway);
        var (caller, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "confirm");
        var intentId = await OpenSetupAsync(caller, gateway, host, clientId, succeeded: true);

        var response = await caller.PostAsJsonAsync(ConfirmRoute, new { setupIntentId = intentId });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var consent = (await ConsentsOfAsync(host, clientId)).Single();
        consent.Status.Should().Be(CardConsentStatus.Active);
        consent.GatewayPaymentMethodId.Should().Be("pm_new_1");
        gateway.LastSetDefaultPaymentMethodId.Should().Be("pm_new_1");
    }

    /// <summary>Confirming twice changes nothing the second time.</summary>
    [Fact]
    public async Task Confirm_Twice_StaysActiveAsync()
    {
        var gateway = new FakeCardPaymentGateway { IsConfigured = true };
        var host = BuildHost(gateway);
        var (caller, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "confirm");
        var intentId = await OpenSetupAsync(caller, gateway, host, clientId, succeeded: true);

        await caller.PostAsJsonAsync(ConfirmRoute, new { setupIntentId = intentId });
        var again = await caller.PostAsJsonAsync(ConfirmRoute, new { setupIntentId = intentId });

        again.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ConsentsOfAsync(host, clientId)).Should().ContainSingle().Which.Status.Should().Be(CardConsentStatus.Active);
    }

    /// <summary>A setup the gateway did not complete is <c>CARD_SETUP_NOT_COMPLETED</c> and the agreement stays pending.</summary>
    [Fact]
    public async Task Confirm_NotSavedAtGateway_RefusesAndStaysPendingAsync()
    {
        var gateway = new FakeCardPaymentGateway { IsConfigured = true };
        var host = BuildHost(gateway);
        var (caller, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "confirm");
        var intentId = await OpenSetupAsync(caller, gateway, host, clientId, succeeded: false);

        var response = await caller.PostAsJsonAsync(ConfirmRoute, new { setupIntentId = intentId });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Contain(HostpanelErrorCodes.CARD_SETUP_NOT_COMPLETED);
        (await ConsentsOfAsync(host, clientId)).Single().Status.Should().Be(CardConsentStatus.Pending);
    }

    /// <summary>A setup intent that belongs to another gateway customer is refused: the card is not this client's.</summary>
    [Fact]
    public async Task Confirm_IntentOfAnotherCustomer_RefusesNotOwnedAsync()
    {
        var gateway = new FakeCardPaymentGateway { IsConfigured = true };
        var host = BuildHost(gateway);
        var (caller, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "confirm");
        var intentId = await OpenSetupAsync(caller, gateway, host, clientId, succeeded: true);
        gateway.SetupVerification = new CardSetupIntentStatus(true, "cus_somebody_else", "pm_new_1");

        var response = await caller.PostAsJsonAsync(ConfirmRoute, new { setupIntentId = intentId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Contain(HostpanelErrorCodes.PAYMENT_METHOD_NOT_OWNED);
        (await ConsentsOfAsync(host, clientId)).Single().Status.Should().Be(CardConsentStatus.Pending);
    }

    /// <summary>A setup intent nobody opened for this client is <c>CARD_SETUP_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task Confirm_UnknownIntent_RefusesNotFoundAsync()
    {
        var host = BuildHost(new FakeCardPaymentGateway { IsConfigured = true });
        var (caller, _) = await BillingTestSeeds.RegisterClientAsync(factory, host, "confirm");

        var response = await caller.PostAsJsonAsync(ConfirmRoute, new { setupIntentId = "seti_never_opened" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Contain(HostpanelErrorCodes.CARD_SETUP_NOT_FOUND);
    }

    /// <summary>A client cannot confirm a setup intent another client opened.</summary>
    [Fact]
    public async Task Confirm_IntentOfAnotherClient_RefusesNotFoundAsync()
    {
        var gateway = new FakeCardPaymentGateway { IsConfigured = true };
        var host = BuildHost(gateway);
        var (owner, ownerId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "confirm");
        var (other, _) = await BillingTestSeeds.RegisterClientAsync(factory, host, "confirm");
        var intentId = await OpenSetupAsync(owner, gateway, host, ownerId, succeeded: true);

        var response = await other.PostAsJsonAsync(ConfirmRoute, new { setupIntentId = intentId });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ConsentsOfAsync(host, ownerId)).Single().Status.Should().Be(CardConsentStatus.Pending);
    }

    /// <summary>A caller without a session is refused.</summary>
    [Fact]
    public async Task Confirm_Anonymous_RefusesUnauthorizedAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(ConfirmRoute, new { setupIntentId = "seti_x" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Opens a setup intent as the client and tells the fake gateway what it will report about it.</summary>
    /// <param name="caller">The client.</param>
    /// <param name="gateway">The fake gateway.</param>
    /// <param name="host">The test host.</param>
    /// <param name="clientId">The client's id.</param>
    /// <param name="succeeded">Whether the gateway reports the card as saved.</param>
    /// <returns>The setup intent id.</returns>
    private static async Task<string> OpenSetupAsync(
        HttpClient caller, FakeCardPaymentGateway gateway, WebApplicationFactory<Program> host, int clientId, bool succeeded)
    {
        var intentId = $"seti_{Guid.NewGuid():N}";
        gateway.SetupIntent = new CardSetupIntentResult(intentId, $"{intentId}_secret");
        var opened = await caller.PostAsJsonAsync(SetupRoute, new { consentAccepted = true });
        opened.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var customer = await context.Clients.AsNoTracking().Where(c => c.Id == clientId).Select(c => c.CardGatewayCustomerId).SingleAsync();
        gateway.SetupVerification = new CardSetupIntentStatus(succeeded, customer, succeeded ? "pm_new_1" : null);
        return intentId;
    }

    /// <summary>Builds a host whose card gateway is <paramref name="gateway"/>.</summary>
    /// <param name="gateway">The fake card gateway.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(FakeCardPaymentGateway gateway) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ICardPaymentGateway>(gateway)));

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
