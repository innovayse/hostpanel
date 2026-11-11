using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>
/// Integration tests for the <c>ScheduleEmailCampaign</c> use case: <c>POST /api/admin/email-campaigns/{id}/schedule</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ScheduleEmailCampaignTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A future moment queues the draft and records that moment.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Schedule_AtAFutureMoment_QueuesTheCampaign()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await CampaignTestSeeds.CreateAsync(client);
        var at = DateTimeOffset.UtcNow.AddDays(2);

        var response = await client.PostAsJsonAsync($"{CampaignTestSeeds.Route}/{id}/schedule", new { at });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await client.GetFromJsonAsync<JsonElement>($"{CampaignTestSeeds.Route}/{id}");
        stored.GetProperty("status").GetString().Should().Be("Scheduled");
        stored.GetProperty("scheduledAt").GetDateTimeOffset().Should().BeCloseTo(at, TimeSpan.FromMilliseconds(1));
    }

    /// <summary>No moment sends now: the campaign is queued, due at the moment of the request.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Schedule_WithoutAMoment_IsDueNow()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await CampaignTestSeeds.CreateAsync(client);
        var before = DateTimeOffset.UtcNow;

        var response = await client.PostAsJsonAsync($"{CampaignTestSeeds.Route}/{id}/schedule", new { at = (DateTimeOffset?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await client.GetFromJsonAsync<JsonElement>($"{CampaignTestSeeds.Route}/{id}");
        stored.GetProperty("status").GetString().Should().Be("Scheduled");
        stored.GetProperty("scheduledAt").GetDateTimeOffset().Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow);
    }

    /// <summary>A moment that has passed is refused with 400 and the in-the-past code; the draft stays a draft.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Schedule_AtAPastMoment_RefusesWithSchedulePastCode()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await CampaignTestSeeds.CreateAsync(client);

        var response = await client.PostAsJsonAsync($"{CampaignTestSeeds.Route}/{id}/schedule", new { at = DateTimeOffset.UtcNow.AddDays(-1) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CAMPAIGN_SCHEDULE_IN_PAST);
        var stored = await client.GetFromJsonAsync<JsonElement>($"{CampaignTestSeeds.Route}/{id}");
        stored.GetProperty("status").GetString().Should().Be("Draft");
    }

    /// <summary>A campaign that reaches nobody is refused with 400 and the no-segment code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Schedule_WithoutSegment_RefusesWithNoSegmentCode()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await CampaignTestSeeds.CreateAsync(client, segments: []);

        var response = await client.PostAsJsonAsync($"{CampaignTestSeeds.Route}/{id}/schedule", new { at = DateTimeOffset.UtcNow.AddDays(1) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CAMPAIGN_NO_SEGMENT);
    }

    /// <summary>An id that names no campaign answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Schedule_UnknownId_RefusesWithNotFoundCode()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync($"{CampaignTestSeeds.Route}/2147483000/schedule", new { at = (DateTimeOffset?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CAMPAIGN_NOT_FOUND);
    }

    /// <summary>A campaign that is already queued cannot be queued again: 409 with the not-a-draft code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Schedule_AlreadyScheduled_RefusesWithNotDraftCode()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await CampaignTestSeeds.CreateAsync(client);
        await CampaignTestSeeds.ScheduleAsync(client, id);

        var response = await client.PostAsJsonAsync($"{CampaignTestSeeds.Route}/{id}/schedule", new { at = DateTimeOffset.UtcNow.AddDays(3) });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CAMPAIGN_NOT_DRAFT);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Schedule_NonPositiveId_RefusesWithValidationError()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync($"{CampaignTestSeeds.Route}/0/schedule", new { at = (DateTimeOffset?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Schedule_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync($"{CampaignTestSeeds.Route}/1/schedule", new { at = (DateTimeOffset?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
