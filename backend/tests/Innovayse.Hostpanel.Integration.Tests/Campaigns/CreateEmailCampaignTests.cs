using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>
/// Integration tests for the <c>CreateEmailCampaign</c> use case: <c>POST /api/admin/email-campaigns</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateEmailCampaignTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Writing answers 201 with the new id and stores a draft whose author is the caller, never a name from the body.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_StoresADraftAuthoredByTheCaller()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(CampaignTestSeeds.Route, new
        {
            subject = "  Autumn offer  ",
            body = "<p>Hello</p>",
            segments = new object[] { new { filterType = "Country", filterValue = "am" } },
            createdBy = "forged-author",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<int>();
        response.Headers.Location!.ToString().Should().EndWith($"{CampaignTestSeeds.Route}/{id}");
        var stored = await client.GetFromJsonAsync<JsonElement>($"{CampaignTestSeeds.Route}/{id}");
        stored.GetProperty("subject").GetString().Should().Be("Autumn offer");
        stored.GetProperty("status").GetString().Should().Be("Draft");
        stored.GetProperty("createdBy").GetString().Should().Be(await CampaignTestSeeds.AdminSubjectAsync(factory));
        stored.GetProperty("scheduledAt").ValueKind.Should().Be(JsonValueKind.Null);
        var segment = stored.GetProperty("segments").EnumerateArray().Single();
        segment.GetProperty("filterType").GetString().Should().Be("Country");
        segment.GetProperty("filterValue").GetString().Should().Be("AM");
    }

    /// <summary>A draft can be written before its audience is chosen.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_WithoutSegments_IsAllowed()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);

        var id = await CampaignTestSeeds.CreateAsync(client, segments: []);

        var stored = await client.GetFromJsonAsync<JsonElement>($"{CampaignTestSeeds.Route}/{id}");
        stored.GetProperty("segments").GetArrayLength().Should().Be(0);
    }

    /// <summary>A body the validator refuses answers 400 <c>VALIDATION_ERROR</c> and stores nothing.</summary>
    /// <param name="subject">The subject line sent.</param>
    /// <param name="body">The body sent.</param>
    /// <param name="filterType">The filter of the one segment sent.</param>
    /// <param name="filterValue">The value of the one segment sent.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("", "<p>Hello</p>", "AllActive", null)]
    [InlineData("   ", "<p>Hello</p>", "AllActive", null)]
    [InlineData("Subject", "", "AllActive", null)]
    [InlineData("Subject", "<p>Hello</p>", "Unknown", null)]
    [InlineData("Subject", "<p>Hello</p>", "AllActive", "5")]
    [InlineData("Subject", "<p>Hello</p>", "Country", "ARM")]
    [InlineData("Subject", "<p>Hello</p>", "Product", "0")]
    [InlineData("Subject", "<p>Hello</p>", "ServiceExpiringInDays", "366")]
    public async Task Create_RefusesAnInvalidField(string subject, string body, string filterType, string? filterValue)
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(CampaignTestSeeds.Route, new
        {
            subject,
            body,
            segments = new object[] { new { filterType, filterValue } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>A subject, a body or a segment list over its limit is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_RefusesValuesOverTheirLimits()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);

        var longSubject = await client.PostAsJsonAsync(CampaignTestSeeds.Route, CampaignTestSeeds.Body(subject: new string('s', 201)));
        var longBody = await client.PostAsJsonAsync(CampaignTestSeeds.Route, CampaignTestSeeds.Body(body: new string('b', 100_001)));
        var manySegments = await client.PostAsJsonAsync(CampaignTestSeeds.Route, CampaignTestSeeds.Body(segments: Enumerable.Repeat(CampaignTestSeeds.AllActive, 51).ToList()));

        longSubject.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        longBody.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        manySegments.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A body with no segment list at all is refused rather than read as an empty one.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_WithoutSegmentList_RefusesWithValidationError()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(CampaignTestSeeds.Route, new { subject = "Subject", body = "<p>Hello</p>" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(CampaignTestSeeds.Route, CampaignTestSeeds.Body());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
