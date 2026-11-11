using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>
/// Integration tests for the <c>GetEmailCampaign</c> use case: <c>GET /api/admin/email-campaigns/{id}</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetEmailCampaignTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A draft comes back whole, with all-zero stats because it has no recipients yet.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_ReturnsTheCampaignWithZeroStats()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await CampaignTestSeeds.CreateAsync(client);

        var campaign = await client.GetFromJsonAsync<JsonElement>($"{CampaignTestSeeds.Route}/{id}");

        campaign.GetProperty("id").GetInt32().Should().Be(id);
        campaign.GetProperty("subject").GetString().Should().Be("Autumn offer");
        campaign.GetProperty("body").GetString().Should().Be("<p>Hello</p>");
        campaign.GetProperty("status").GetString().Should().Be("Draft");
        campaign.GetProperty("createdAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5));
        campaign.GetProperty("sentAt").ValueKind.Should().Be(JsonValueKind.Null);
        campaign.GetProperty("segments").GetArrayLength().Should().Be(1);
        var stats = campaign.GetProperty("stats");
        new[] { "total", "pending", "sent", "failed", "unsubscribed" }.Select(name => stats.GetProperty(name).GetInt32()).Should().AllBeEquivalentTo(0);
    }

    /// <summary>Once sending has fixed the recipients the stats count each delivery state.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_CountsRecipientsByDeliveryState()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await CampaignTestSeeds.CreateAsync(client);
        await CampaignTestSeeds.ScheduleAsync(client, id);
        await CampaignTestSeeds.SeedSendingWithRecipientsAsync(factory, id);

        var campaign = await client.GetFromJsonAsync<JsonElement>($"{CampaignTestSeeds.Route}/{id}");

        campaign.GetProperty("status").GetString().Should().Be("Sending");
        var stats = campaign.GetProperty("stats");
        stats.GetProperty("total").GetInt32().Should().Be(4);
        stats.GetProperty("pending").GetInt32().Should().Be(1);
        stats.GetProperty("sent").GetInt32().Should().Be(1);
        stats.GetProperty("failed").GetInt32().Should().Be(1);
        stats.GetProperty("unsubscribed").GetInt32().Should().Be(1);
    }

    /// <summary>An id that names no campaign answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_UnknownId_RefusesWithNotFoundCode()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync($"{CampaignTestSeeds.Route}/2147483000");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CAMPAIGN_NOT_FOUND);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_NonPositiveId_RefusesWithValidationError()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync($"{CampaignTestSeeds.Route}/0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync($"{CampaignTestSeeds.Route}/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
