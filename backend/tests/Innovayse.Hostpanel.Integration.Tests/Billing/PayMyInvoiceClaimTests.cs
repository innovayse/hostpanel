using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.StoredCredentials;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/me/invoices/{id}/pay</c> and the invoice payment claim it takes before any money moves: a stale claim
/// is looked up at the gateway before a new attempt may charge, and only the claim's unique index counts as another
/// payment being in progress, never an unrelated save failure.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PayMyInvoiceClaimTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The saved payment method every seeded customer owns in these tests.</summary>
    private const string CardId = "pm_claim_1";

    /// <summary>Registers a client with a customer and a card on file and raises an unpaid invoice for them.</summary>
    /// <param name="gateway">The fake capability host.</param>
    /// <param name="label">Distinguishes the seeded email between tests.</param>
    /// <returns>The client's caller, the client id, the gateway customer and the invoice id.</returns>
    private async Task<(HttpClient Owner, string CustomerRef, int InvoiceId)> SeedAsync(StoredCredentialHost gateway, string label)
    {
        gateway.Plugin.Methods.Add(StoredCredentialHost.Card(CardId));
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, label);
        var customerRef = $"cus_claim_{Guid.NewGuid():N}"[..24];
        await gateway.LinkAsync(clientId, customerRef);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, gateway.Host);
        return (owner, customerRef, await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m));
    }

    /// <summary>Pays an invoice by hand under a new attempt.</summary>
    /// <param name="owner">The paying client.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The response.</returns>
    private static Task<HttpResponseMessage> PayAsync(HttpClient owner, int invoiceId) =>
        owner.PostAsJsonAsync(
            $"/api/me/invoices/{invoiceId}/pay",
            new { gatewayModuleId = StoredCredentialHost.Module, paymentMethodId = CardId, attemptId = Guid.NewGuid() });

    /// <summary>Adds an unfinished claim that started long ago.</summary>
    /// <param name="host">The host whose database to write.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="key">The claim's gateway key.</param>
    /// <returns>A task that completes when the claim is stored.</returns>
    private static async Task AddStaleClaimAsync(WebApplicationFactory<Program> host, int invoiceId, string key)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        context.InvoicePaymentClaims.Add(InvoicePaymentClaim.Create(
            invoiceId, StoredCredentialHost.Module, InvoicePaymentClaimKind.Manual, key, DateTimeOffset.UtcNow.AddHours(-2), TimeSpan.FromMinutes(15)));
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Reads the payment claims of an invoice.</summary>
    /// <param name="host">The host whose database to read.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The claims, oldest first.</returns>
    private static async Task<List<InvoicePaymentClaim>> ClaimsOfAsync(WebApplicationFactory<Program> host, int invoiceId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.InvoicePaymentClaims.AsNoTracking().Where(c => c.InvoiceId == invoiceId).OrderBy(c => c.Id).ToListAsync();
    }

    /// <summary>
    /// An earlier attempt took the money and died before the invoice was settled: the next pay action looks the
    /// charge up by the old key, settles the invoice from it and answers success without charging a second time.
    /// </summary>
    [Fact]
    public async Task Pay_StaleClaimWhoseChargeWentThrough_SettlesWithoutChargingAgainAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        var (owner, customerRef, invoiceId) = await SeedAsync(gateway, "claim-stale-found");
        var staleKey = $"invoice-{invoiceId}-attempt-{Guid.NewGuid():N}";
        gateway.Plugin.Charges[staleKey] = (
            new StoredChargeInstruction(customerRef, CardId, 1000, "USD", invoiceId.ToString(System.Globalization.CultureInfo.InvariantCulture), staleKey, null),
            new StoredChargeResult(StoredChargeOutcome.Succeeded, $"txn-stale-{Guid.NewGuid():N}", 1000, "USD", null));
        await AddStaleClaimAsync(gateway.Host, invoiceId, staleKey);

        var response = await PayAsync(owner, invoiceId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        gateway.Plugin.Charges.Should().ContainSingle();
        (await ClaimsOfAsync(gateway.Host, invoiceId)).Should().ContainSingle().Which.Outcome.Should().Be(InvoicePaymentClaimOutcome.Settled);
    }

    /// <summary>A stale claim the gateway has no charge for is expired and the new attempt charges.</summary>
    [Fact]
    public async Task Pay_StaleClaimWithoutACharge_ExpiresItAndChargesAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        var (owner, _, invoiceId) = await SeedAsync(gateway, "claim-stale-none");
        await AddStaleClaimAsync(gateway.Host, invoiceId, $"invoice-{invoiceId}-attempt-{Guid.NewGuid():N}");

        var response = await PayAsync(owner, invoiceId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        gateway.Plugin.Charges.Should().ContainSingle();
        (await ClaimsOfAsync(gateway.Host, invoiceId)).Select(c => c.Outcome).Should()
            .BeEquivalentTo([InvoicePaymentClaimOutcome.Expired, InvoicePaymentClaimOutcome.Settled]);
    }

    /// <summary>A stale claim whose lookup fails keeps the invoice held: the payer is told it is in progress and nothing is charged.</summary>
    [Fact]
    public async Task Pay_StaleClaimWhenTheLookupFails_AnswersInProgressWithoutChargingAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        var (owner, _, invoiceId) = await SeedAsync(gateway, "claim-stale-lookup");
        await AddStaleClaimAsync(gateway.Host, invoiceId, $"invoice-{invoiceId}-attempt-{Guid.NewGuid():N}");
        gateway.Plugin.LookupThrows = true;

        var response = await PayAsync(owner, invoiceId);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Contain(HostpanelErrorCodes.PAYMENT_IN_PROGRESS);
        gateway.Plugin.Charges.Should().BeEmpty();
        (await ClaimsOfAsync(gateway.Host, invoiceId)).Should().ContainSingle().Which.Outcome.Should().BeNull();
    }

    /// <summary>A fresh claim held by another attempt makes this one wait: it is told the payment is in progress and charges nothing.</summary>
    [Fact]
    public async Task Pay_WhileAnotherAttemptHoldsAFreshClaim_AnswersInProgressAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        var (owner, _, invoiceId) = await SeedAsync(gateway, "claim-fresh");
        using (var scope = gateway.Host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            context.InvoicePaymentClaims.Add(InvoicePaymentClaim.Create(
                invoiceId, StoredCredentialHost.Module, InvoicePaymentClaimKind.Automatic, "held-by-the-job", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(15)));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var response = await PayAsync(owner, invoiceId);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Contain(HostpanelErrorCodes.PAYMENT_IN_PROGRESS);
        gateway.Plugin.Charges.Should().BeEmpty();
    }

    /// <summary>
    /// Only the claim's unique index means another payment took the invoice. Any other failure while saving the claim is
    /// a real error: it surfaces as a server error instead of being reported as "payment in progress", and nothing is charged.
    /// </summary>
    [Fact]
    public async Task Pay_UnrelatedSaveFailureWhileTakingTheClaim_IsNotReportedAsBusyAsync()
    {
        var failing = new ClaimTakenInterceptor(_ => throw new DbUpdateException("The database refused the claim for another reason."));
        var gateway = StoredCredentialHost.Create(factory, failing);
        var (owner, _, invoiceId) = await SeedAsync(gateway, "claim-save-fails");

        var response = await PayAsync(owner, invoiceId);

        failing.Fired.Should().BeTrue();
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        gateway.Plugin.Charges.Should().BeEmpty();
        (await ClaimsOfAsync(gateway.Host, invoiceId)).Should().BeEmpty();
    }
}
