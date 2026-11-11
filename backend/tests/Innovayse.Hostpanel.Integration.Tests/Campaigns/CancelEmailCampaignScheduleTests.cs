using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>
/// Integration tests for the <c>CancelEmailCampaignSchedule</c> use case: <c>POST /api/admin/email-campaigns/{id}/cancel</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CancelEmailCampaignScheduleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Cancelling takes a queued campaign back to a draft with no moment, so it can be edited again.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Cancel_ReturnsAScheduledCampaignToDraft()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await CampaignTestSeeds.CreateAsync(client);
        await CampaignTestSeeds.ScheduleAsync(client, id);

        var response = await client.PostAsync($"{CampaignTestSeeds.Route}/{id}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await client.GetFromJsonAsync<JsonElement>($"{CampaignTestSeeds.Route}/{id}");
        stored.GetProperty("status").GetString().Should().Be("Draft");
        stored.GetProperty("scheduledAt").ValueKind.Should().Be(JsonValueKind.Null);
        (await client.PutAsJsonAsync($"{CampaignTestSeeds.Route}/{id}", CampaignTestSeeds.Body(subject: "Edited again"))).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>A draft has no schedule to cancel: 409 with the not-scheduled code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Cancel_Draft_RefusesWithNotScheduledCode()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await CampaignTestSeeds.CreateAsync(client);

        var response = await client.PostAsync($"{CampaignTestSeeds.Route}/{id}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CAMPAIGN_NOT_SCHEDULED);
    }

    /// <summary>A campaign that has started sending can no longer be taken back: 409 with the not-scheduled code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Cancel_Sending_RefusesWithNotScheduledCode()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await CampaignTestSeeds.CreateAsync(client);
        await CampaignTestSeeds.ScheduleAsync(client, id);
        await CampaignTestSeeds.SeedSendingWithRecipientsAsync(factory, id);

        var response = await client.PostAsync($"{CampaignTestSeeds.Route}/{id}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CAMPAIGN_NOT_SCHEDULED);
    }

    /// <summary>An id that names no campaign answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Cancel_UnknownId_RefusesWithNotFoundCode()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsync($"{CampaignTestSeeds.Route}/2147483000/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CAMPAIGN_NOT_FOUND);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Cancel_NonPositiveId_RefusesWithValidationError()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsync($"{CampaignTestSeeds.Route}/0/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Cancel_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsync($"{CampaignTestSeeds.Route}/1/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
