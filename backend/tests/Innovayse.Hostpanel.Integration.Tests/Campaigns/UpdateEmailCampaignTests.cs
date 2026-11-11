using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>
/// Integration tests for the <c>UpdateEmailCampaign</c> use case: <c>PUT /api/admin/email-campaigns/{id}</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateEmailCampaignTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Updating replaces the subject, the body and the whole segment set, and the route id wins over the body's.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_ReplacesContentAndSegments()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await CampaignTestSeeds.CreateAsync(client);
        var other = await CampaignTestSeeds.CreateAsync(client, subject: "Untouched");

        var response = await client.PutAsJsonAsync($"{CampaignTestSeeds.Route}/{id}", new
        {
            id = other,
            subject = "Winter offer",
            body = "<p>Edited</p>",
            segments = new object[] { new { filterType = "Product", filterValue = "7" }, new { filterType = "ServiceExpiringInDays", filterValue = "30" } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var edited = await client.GetFromJsonAsync<JsonElement>($"{CampaignTestSeeds.Route}/{id}");
        edited.GetProperty("subject").GetString().Should().Be("Winter offer");
        edited.GetProperty("body").GetString().Should().Be("<p>Edited</p>");
        edited.GetProperty("segments").EnumerateArray().Select(s => s.GetProperty("filterType").GetString())
            .Should().BeEquivalentTo("Product", "ServiceExpiringInDays");
        var untouched = await client.GetFromJsonAsync<JsonElement>($"{CampaignTestSeeds.Route}/{other}");
        untouched.GetProperty("subject").GetString().Should().Be("Untouched");
    }

    /// <summary>An id that names no campaign answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_UnknownId_RefusesWithNotFoundCode()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"{CampaignTestSeeds.Route}/2147483000", CampaignTestSeeds.Body());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CAMPAIGN_NOT_FOUND);
    }

    /// <summary>A campaign that is queued or sent is no longer edited: 409 with the not-a-draft code, and nothing changes.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_AfterScheduling_RefusesWithNotDraftCode()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await CampaignTestSeeds.CreateAsync(client);
        await CampaignTestSeeds.ScheduleAsync(client, id);

        var response = await client.PutAsJsonAsync($"{CampaignTestSeeds.Route}/{id}", CampaignTestSeeds.Body(subject: "Too late"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CAMPAIGN_NOT_DRAFT);
        var stored = await client.GetFromJsonAsync<JsonElement>($"{CampaignTestSeeds.Route}/{id}");
        stored.GetProperty("subject").GetString().Should().Be("Autumn offer");
    }

    /// <summary>A refused field answers 400 <c>VALIDATION_ERROR</c> and leaves the draft as it was.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_InvalidSegment_RefusesWithValidationError()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await CampaignTestSeeds.CreateAsync(client);

        var response = await client.PutAsJsonAsync(
            $"{CampaignTestSeeds.Route}/{id}",
            CampaignTestSeeds.Body(segments: [new { filterType = "Country", filterValue = "ARM" }]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
        var stored = await client.GetFromJsonAsync<JsonElement>($"{CampaignTestSeeds.Route}/{id}");
        stored.GetProperty("segments").EnumerateArray().Single().GetProperty("filterType").GetString().Should().Be("AllActive");
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync($"{CampaignTestSeeds.Route}/1", CampaignTestSeeds.Body());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
