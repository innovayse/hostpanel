using System.Net;
using Innovayse.Hostpanel.Application.Billing.Commands.ChargeInvoiceStoredMethod;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>DELETE /api/me/stored-payment-methods/consents/{id}</c> against Postgres: a revoked consent is final, the host
/// never charges the method again, the method is removed at the gateway, and a consent of someone else's is not found.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RevokeCardChargeConsentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Revoking stamps the consent, stops charges and removes the method at the gateway.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Revoke_OwnConsent_StopsChargesAndRemovesTheMethod()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "revoke-ok");
        var consentId = await scenario.SeedConsentAsync(signedIn.ClientId);
        var customerRef = $"demo-cus-client-{signedIn.ClientId}";
        var session = await scenario.Plugin.BeginSaveMethodAsync(new(customerRef, "https://client.test/x"), CancellationToken.None);
        var saved = await scenario.Plugin.CompleteSaveMethodAsync(session.SessionId, CancellationToken.None);
        scenario.State.Methods(customerRef).Should().ContainSingle();
        await SetMethodAsync(scenario, consentId, saved.Method!.MethodRef);
        var invoiceId = await scenario.SeedInvoiceAsync(signedIn.ClientId, 10.00m);

        var response = await signedIn.Client.DeleteAsync($"/api/me/stored-payment-methods/consents/{consentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = scenario.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.CardChargeConsents.AsNoTracking().SingleAsync(c => c.Id == consentId)).RevokedAt.Should().NotBeNull();
        scenario.State.Methods(customerRef).Should().BeEmpty("the method is removed at the gateway");
        var charge = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId));
        charge.ErrorCode.Should().Be(HostpanelErrorCodes.CARD_CONSENT_REQUIRED);
    }

    /// <summary>Revoking again answers 204 and changes nothing: a revoked consent stays revoked.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Revoke_Twice_IsIdempotent()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "revoke-twice");
        var consentId = await scenario.SeedConsentAsync(signedIn.ClientId);
        await signedIn.Client.DeleteAsync($"/api/me/stored-payment-methods/consents/{consentId}");
        DateTimeOffset? first;
        using (var scope = scenario.Host.Services.CreateScope())
        {
            first = (await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().CardChargeConsents.AsNoTracking()
                .SingleAsync(c => c.Id == consentId)).RevokedAt;
        }

        var again = await signedIn.Client.DeleteAsync($"/api/me/stored-payment-methods/consents/{consentId}");

        again.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var check = scenario.Host.Services.CreateScope();
        (await check.ServiceProvider.GetRequiredService<IHostpanelDbContext>().CardChargeConsents.AsNoTracking()
            .SingleAsync(c => c.Id == consentId)).RevokedAt.Should().Be(first);
    }

    /// <summary>Another client's consent is not found, and stays active.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Revoke_AnotherClientsConsent_IsNotFound()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var owner = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "revoke-owner");
        var stranger = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "revoke-stranger");
        var consentId = await scenario.SeedConsentAsync(owner.ClientId);

        var response = await stranger.Client.DeleteAsync($"/api/me/stored-payment-methods/consents/{consentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CARD_CONSENT_NOT_FOUND);
        using var scope = scenario.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.CardChargeConsents.AsNoTracking().SingleAsync(c => c.Id == consentId)).IsActive.Should().BeTrue();
    }

    /// <summary>
    /// Points a seeded consent at a method the gateway really holds, so removing it can be observed. The seeded consent
    /// covers a fixed method reference, which the demo gateway does not hold for the customer.
    /// </summary>
    /// <param name="scenario">The scenario whose database to change.</param>
    /// <param name="consentId">The consent.</param>
    /// <param name="methodRef">The method the gateway holds.</param>
    /// <returns>A task that completes once the change is saved.</returns>
    private static async Task SetMethodAsync(StoredCredentialScenario scenario, int consentId, string methodRef)
    {
        using var scope = scenario.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Innovayse.Hostpanel.Infrastructure.Persistence.AppDbContext>();
        await context.CardChargeConsents.Where(c => c.Id == consentId)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.MethodRef, methodRef));
    }
}
