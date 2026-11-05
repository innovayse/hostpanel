using System.Net;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>Integration tests for <c>GET /api/billing/renewal-claims</c>: the domain renewal lines left claimed and unapplied.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListClaimedDomainRenewalsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Reads the claims list.</summary>
    /// <returns>The listed lines as JSON objects, oldest claim first.</returns>
    private async Task<List<JsonElement>> ListAsync()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var response = await admin.GetAsync("/api/billing/renewal-claims");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    /// <summary>A claimed line is listed with its invoice, client and domain, oldest claim first.</summary>
    [Fact]
    public async Task AClaimedLine_IsListed_OldestFirst()
    {
        var older = await ClaimedRenewalSeeds.SeedAsync(factory, claimedAt: DateTimeOffset.UtcNow.AddDays(-30));
        var newer = await ClaimedRenewalSeeds.SeedAsync(factory, claimedAt: DateTimeOffset.UtcNow.AddDays(-1));

        var listed = await ListAsync();

        var ids = listed.Select(e => e.GetProperty("itemId").GetInt32()).ToList();
        ids.Should().Contain([older.ItemId, newer.ItemId]);
        ids.IndexOf(older.ItemId).Should().BeLessThan(ids.IndexOf(newer.ItemId));
        var row = listed.Single(e => e.GetProperty("itemId").GetInt32() == older.ItemId);
        row.GetProperty("invoiceId").GetInt32().Should().Be(older.InvoiceId);
        row.GetProperty("clientId").GetInt32().Should().Be(older.ClientId);
    }

    /// <summary>A line nobody claimed is not listed.</summary>
    [Fact]
    public async Task AnUnclaimedLine_IsNotListed()
    {
        var line = await ClaimedRenewalSeeds.SeedAsync(factory, claim: false);

        (await ListAsync()).Select(e => e.GetProperty("itemId").GetInt32()).Should().NotContain(line.ItemId);
    }

    /// <summary>Anonymous callers get 401 and a client who is not staff gets 403.</summary>
    [Fact]
    public async Task OnlyStaffMayList()
    {
        var stranger = await ServicesTestSeeds.RegisterStrangerAsync(factory);

        (await factory.CreateClient().GetAsync("/api/billing/renewal-claims")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await stranger.GetAsync("/api/billing/renewal-claims")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
