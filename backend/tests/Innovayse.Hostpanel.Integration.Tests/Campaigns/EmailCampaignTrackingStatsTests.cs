using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>
/// Integration tests for the open and click figures in a campaign's stats, read through <c>GET /api/admin/email-campaigns/{id}</c>
/// and the list: totals, unique counts, and rates taken over the mails that were delivered.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class EmailCampaignTrackingStatsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The names of the tracking figures in the stats object.</summary>
    private static readonly string[] TrackingFigures = ["opened", "uniqueOpened", "clicked", "uniqueClicked", "openRate", "clickRate"];

    /// <summary>A campaign with no recipient reports zero for every tracking figure, the rates included.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_WithoutRecipients_ReportsZeroTracking()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await CampaignTestSeeds.CreateAsync(client);

        var stats = (await client.GetFromJsonAsync<JsonElement>($"{CampaignTestSeeds.Route}/{id}")).GetProperty("stats");

        TrackingFigures.Select(name => stats.GetProperty(name).GetDouble()).Should().AllBeEquivalentTo(0);
    }

    /// <summary>
    /// Totals count every open and click, unique counts count readers, and the rates divide by the delivered mails only: a
    /// recipient that failed or unsubscribed before delivery is not in the denominator.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_CountsOpensAndClicks_AndRatesThemOverTheDeliveredMails()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await CampaignTestSeeds.CreateAsync(client);
        await CampaignTestSeeds.ScheduleAsync(client, id);
        await CampaignTestSeeds.SeedSendingWithRecipientsAsync(factory, id);
        await RecordResponsesAsync(id);

        var campaign = await client.GetFromJsonAsync<JsonElement>($"{CampaignTestSeeds.Route}/{id}");

        var stats = campaign.GetProperty("stats");
        stats.GetProperty("sent").GetInt32().Should().Be(2);
        stats.GetProperty("opened").GetInt32().Should().Be(3);
        stats.GetProperty("uniqueOpened").GetInt32().Should().Be(1);
        stats.GetProperty("clicked").GetInt32().Should().Be(1);
        stats.GetProperty("uniqueClicked").GetInt32().Should().Be(1);
        stats.GetProperty("openRate").GetDouble().Should().Be(0.5);
        stats.GetProperty("clickRate").GetDouble().Should().Be(0.5);
    }

    /// <summary>
    /// A mail opened and clicked without a delivery moment (the sender saves the delivery after the mail has left) is in the
    /// totals but is not a reader of a delivered mail, so the rates stay over the delivered mails and cannot pass 1.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_OpenOfAMailNotDelivered_DoesNotLiftTheRatesPastTheDeliveredMails()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await CampaignTestSeeds.CreateAsync(client);
        await CampaignTestSeeds.ScheduleAsync(client, id);
        await CampaignTestSeeds.SeedSendingWithRecipientsAsync(factory, id);
        await RecordResponsesAsync(id);
        await RecordResponsesOfUndeliveredAsync(id);

        var stats = (await client.GetFromJsonAsync<JsonElement>($"{CampaignTestSeeds.Route}/{id}")).GetProperty("stats");

        stats.GetProperty("opened").GetInt32().Should().Be(4);
        stats.GetProperty("uniqueOpened").GetInt32().Should().Be(1);
        stats.GetProperty("uniqueClicked").GetInt32().Should().Be(1);
        stats.GetProperty("openRate").GetDouble().Should().Be(0.5);
        stats.GetProperty("clickRate").GetDouble().Should().Be(0.5);
    }

    /// <summary>The list rows carry the same tracking figures as the detail.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_CarriesTheTrackingFigures()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await CampaignTestSeeds.CreateAsync(client, subject: "Tracked list " + Guid.NewGuid().ToString("N"));
        await CampaignTestSeeds.ScheduleAsync(client, id);
        await CampaignTestSeeds.SeedSendingWithRecipientsAsync(factory, id);
        await RecordResponsesAsync(id);

        var rows = await client.GetFromJsonAsync<JsonElement>(CampaignTestSeeds.Route);

        var row = rows.EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == id);
        var stats = row.GetProperty("stats");
        stats.GetProperty("opened").GetInt32().Should().Be(3);
        stats.GetProperty("uniqueClicked").GetInt32().Should().Be(1);
        stats.GetProperty("openRate").GetDouble().Should().Be(0.5);
    }

    /// <summary>Lets the recipient whose delivery failed open its mail and follow a link, though no delivery moment was stored.</summary>
    /// <param name="campaignId">The sending campaign.</param>
    /// <returns>A task that completes once the responses are stored.</returns>
    private async Task RecordResponsesOfUndeliveredAsync(int campaignId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow;

        var undelivered = await db.EmailCampaignRecipients.SingleAsync(r => r.CampaignId == campaignId && r.Email == "failed@example.test");
        undelivered.RecordOpen(now);
        undelivered.RecordClick(now);
        await db.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Delivers the campaign's pending recipient too, so two mails are delivered, then lets one reader open its mail three times
    /// and click once while the other does nothing.
    /// </summary>
    /// <param name="campaignId">The sending campaign.</param>
    /// <returns>A task that completes once the responses are stored.</returns>
    private async Task RecordResponsesAsync(int campaignId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow;
        var recipients = await db.EmailCampaignRecipients.Where(r => r.CampaignId == campaignId).ToListAsync();

        recipients.Single(r => r.Email == "pending@example.test").MarkSent(now);
        var reader = recipients.Single(r => r.Email == "sent@example.test");
        reader.RecordOpen(now);
        reader.RecordOpen(now);
        reader.RecordOpen(now);
        reader.RecordClick(now);
        await db.SaveChangesAsync(CancellationToken.None);
    }
}
