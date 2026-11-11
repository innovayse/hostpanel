using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>
/// Integration tests for the <c>PreviewEmailCampaign</c> use case: <c>GET /api/admin/email-campaigns/{id}/preview</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PreviewEmailCampaignTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The preview is the campaign's subject and its body, printed as written, above a footer with a sample unsubscribe link.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Preview_SavedCampaign_RendersTheBodyAndASampleUnsubscribeLink()
    {
        using (var scope = factory.Services.CreateScope())
        {
            await CampaignTestSeeds.SeedTemplateAsync(scope.ServiceProvider);
        }

        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var created = await admin.PostAsJsonAsync(CampaignTestSeeds.Route, CampaignTestSeeds.Body("Winter <sale>", "<p>Save <b>20%</b></p>"));
        var id = await created.Content.ReadFromJsonAsync<int>();

        var response = await admin.GetAsync($"{CampaignTestSeeds.Route}/{id}/preview");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
        preview.GetProperty("subject").GetString().Should().Be("Winter <sale>");
        preview.GetProperty("html").GetString().Should().Contain("<p>Save <b>20%</b></p>").And.Contain("/unsubscribe?token=sample");
    }

    /// <summary>An id that names no campaign answers 404 with <c>CAMPAIGN_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Preview_UnknownCampaign_RefusesNotFound()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"{CampaignTestSeeds.Route}/999999/preview");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CAMPAIGN_NOT_FOUND);
    }

    /// <summary>A non-positive id is refused with 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Preview_NonPositiveId_RefusesValidation()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"{CampaignTestSeeds.Route}/0/preview");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Preview_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync($"{CampaignTestSeeds.Route}/1/preview");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
