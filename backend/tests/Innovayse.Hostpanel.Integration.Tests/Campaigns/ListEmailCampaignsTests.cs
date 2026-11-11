using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>
/// Integration tests for the <c>ListEmailCampaigns</c> use case: <c>GET /api/admin/email-campaigns</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListEmailCampaignsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The list holds every campaign, the newest first.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ReturnsEveryCampaign_NewestFirst()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var older = await CampaignTestSeeds.CreateAsync(client, subject: "Older");
        var newer = await CampaignTestSeeds.CreateAsync(client, subject: "Newer");

        var list = await client.GetFromJsonAsync<JsonElement[]>(CampaignTestSeeds.Route);

        var ids = list!.Select(x => x.GetProperty("id").GetInt32()).ToList();
        ids.Should().Contain(new[] { older, newer });
        ids.IndexOf(newer).Should().BeLessThan(ids.IndexOf(older));
    }

    /// <summary>A row carries the campaign without its body and segments, and the stats of its own recipients only.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_RowCarriesItsOwnStats()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var draft = await CampaignTestSeeds.CreateAsync(client, subject: "Draft row");
        var sending = await CampaignTestSeeds.CreateAsync(client, subject: "Sending row");
        await CampaignTestSeeds.ScheduleAsync(client, sending);
        await CampaignTestSeeds.SeedSendingWithRecipientsAsync(factory, sending);

        var list = await client.GetFromJsonAsync<JsonElement[]>(CampaignTestSeeds.Route);

        var draftRow = list!.Single(x => x.GetProperty("id").GetInt32() == draft);
        draftRow.GetProperty("status").GetString().Should().Be("Draft");
        draftRow.GetProperty("stats").GetProperty("total").GetInt32().Should().Be(0);
        draftRow.TryGetProperty("body", out _).Should().BeFalse();
        draftRow.TryGetProperty("segments", out _).Should().BeFalse();
        var sendingRow = list!.Single(x => x.GetProperty("id").GetInt32() == sending);
        sendingRow.GetProperty("status").GetString().Should().Be("Sending");
        sendingRow.GetProperty("scheduledAt").GetDateTimeOffset().Should().BeAfter(DateTimeOffset.UtcNow);
        var stats = sendingRow.GetProperty("stats");
        stats.GetProperty("total").GetInt32().Should().Be(4);
        stats.GetProperty("pending").GetInt32().Should().Be(1);
        stats.GetProperty("sent").GetInt32().Should().Be(1);
        stats.GetProperty("failed").GetInt32().Should().Be(1);
        stats.GetProperty("unsubscribed").GetInt32().Should().Be(1);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync(CampaignTestSeeds.Route);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
