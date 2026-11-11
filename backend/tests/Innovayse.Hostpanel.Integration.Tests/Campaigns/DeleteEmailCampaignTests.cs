using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>
/// Integration tests for the <c>DeleteEmailCampaign</c> use case: <c>DELETE /api/admin/email-campaigns/{id}</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteEmailCampaignTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A draft is deleted and is gone afterwards.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_RemovesADraft()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await CampaignTestSeeds.CreateAsync(client);

        var response = await client.DeleteAsync($"{CampaignTestSeeds.Route}/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"{CampaignTestSeeds.Route}/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>An id that names no campaign answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_UnknownId_RefusesWithNotFoundCode()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync($"{CampaignTestSeeds.Route}/2147483000");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CAMPAIGN_NOT_FOUND);
    }

    /// <summary>A queued campaign is kept: 409 with the not-a-draft code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_Scheduled_RefusesWithNotDraftCode()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await CampaignTestSeeds.CreateAsync(client);
        await CampaignTestSeeds.ScheduleAsync(client, id);

        var response = await client.DeleteAsync($"{CampaignTestSeeds.Route}/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CAMPAIGN_NOT_DRAFT);
        (await client.GetAsync($"{CampaignTestSeeds.Route}/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A campaign whose mail has gone out is kept with its recipients: 409 with the not-a-draft code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_Sent_RefusesWithNotDraftCode()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await CampaignTestSeeds.CreateAsync(client);
        await CampaignTestSeeds.ScheduleAsync(client, id);
        await CampaignTestSeeds.SeedSendingWithRecipientsAsync(factory, id);
        await CampaignTestSeeds.SeedSentAsync(factory, id);

        var response = await client.DeleteAsync($"{CampaignTestSeeds.Route}/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CAMPAIGN_NOT_DRAFT);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_NonPositiveId_RefusesWithValidationError()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync($"{CampaignTestSeeds.Route}/0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_RefusesAnonymous()
    {
        var response = await factory.CreateClient().DeleteAsync($"{CampaignTestSeeds.Route}/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
