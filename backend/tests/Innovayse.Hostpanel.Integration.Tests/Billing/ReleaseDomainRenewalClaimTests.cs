using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// Integration tests for the staff release of a domain renewal line left claimed after an unknown registrar
/// outcome: <c>POST /api/billing/{id}/items/{itemId}/release-renewal-claim</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ReleaseDomainRenewalClaimTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Reads the claim of a line as stored.</summary>
    /// <param name="itemId">The line.</param>
    /// <returns>The claim moment, or <see langword="null"/>.</returns>
    private async Task<DateTimeOffset?> ClaimOfAsync(int itemId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.InvoiceItems.AsNoTracking().Where(l => l.Id == itemId).Select(l => l.RenewalAttemptAt).SingleAsync();
    }

    /// <summary>A claimed line is released with 204 and the release is logged.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Release_ClearsTheClaim_AndLogsIt()
    {
        var line = await ClaimedRenewalSeeds.SeedAsync(factory);
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/billing/{line.InvoiceId}/items/{line.ItemId}/release-renewal-claim", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ClaimOfAsync(line.ItemId)).Should().BeNull();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var log = await context.ActivityLogs.AsNoTracking().SingleAsync(
            l => l.ClientId == line.ClientId && l.Description.Contains("Released Domain Renewal Claim"));
        log.Description.Should().Contain($"Invoice ID: {line.InvoiceId}").And.Contain($"Item ID: {line.ItemId}");
    }

    /// <summary>Releasing twice: the second call finds no claim and answers 409 <c>RENEWAL_CLAIM_NOT_HELD</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Release_Twice_Returns409()
    {
        var line = await ClaimedRenewalSeeds.SeedAsync(factory);
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/billing/{line.InvoiceId}/items/{line.ItemId}/release-renewal-claim", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var again = await admin.PostAsync($"/api/billing/{line.InvoiceId}/items/{line.ItemId}/release-renewal-claim", null);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ServicesTestSeeds.CodeOfAsync(again)).Should().Be(HostpanelErrorCodes.RENEWAL_CLAIM_NOT_HELD);
    }

    /// <summary>A line nobody claimed cannot be released.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnUnclaimedLine_CannotBeReleased()
    {
        var line = await ClaimedRenewalSeeds.SeedAsync(factory, claim: false);
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/billing/{line.InvoiceId}/items/{line.ItemId}/release-renewal-claim", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.RENEWAL_CLAIM_NOT_HELD);
    }

    /// <summary>An unknown invoice answers 404 <c>INVOICE_NOT_FOUND</c>, and a line of another invoice answers 404 <c>INVOICE_ITEM_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownInvoiceOrLine_Returns404()
    {
        var line = await ClaimedRenewalSeeds.SeedAsync(factory);
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var noInvoice = await admin.PostAsync($"/api/billing/{BillingTestSeeds.NoSuchId}/items/{line.ItemId}/release-renewal-claim", null);
        var noLine = await admin.PostAsync($"/api/billing/{line.InvoiceId}/items/{BillingTestSeeds.NoSuchId}/release-renewal-claim", null);

        noInvoice.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(noInvoice)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        noLine.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(noLine)).Should().Be(HostpanelErrorCodes.INVOICE_ITEM_NOT_FOUND);
        (await ClaimOfAsync(line.ItemId)).Should().NotBeNull("a refused release changes nothing");
    }

    /// <summary>Anonymous callers get 401 and a client who is not staff gets 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OnlyStaffMayRelease()
    {
        var stranger = await ServicesTestSeeds.RegisterStrangerAsync(factory);

        (await factory.CreateClient().PostAsync("/api/billing/1/items/1/release-renewal-claim", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await stranger.PostAsync("/api/billing/1/items/1/release-renewal-claim", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
