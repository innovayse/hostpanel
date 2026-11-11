using System.Net;
using System.Text.RegularExpressions;
using Innovayse.Hostpanel.Application.Campaigns.Commands.DeliverEmailCampaign;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Campaigns;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>
/// Integration tests for the <c>RecordEmailCampaignClick</c> use case: <c>GET /api/public/campaign-mail/click/{token}/{index}</c>,
/// the anonymous address every tracked link of a campaign mail points at. The security property is the point of most of them:
/// the destination comes only from the campaign's stored body, by index, and is only ever an absolute http or https address.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RecordEmailCampaignClickTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A body with two web links, a mail link and an unsafe link, in that trackable order: offer, other.</summary>
    private const string Body =
        "<p><a href=\"https://shop.example/offer?a=1&amp;b=2\">Offer</a> <a href=\"mailto:sales@example.com\">Write</a> "
        + "<a href=\"javascript:alert(1)\">Nope</a> <a href=\"http://shop.example/other\">Other</a></p>";

    /// <summary>Builds the click route for a token and an index.</summary>
    /// <param name="token">The token to put in the route.</param>
    /// <param name="index">The link index to put in the route.</param>
    /// <returns>The route.</returns>
    private static string Route(string token, int index) => $"/api/public/campaign-mail/click/{token}/{index}";

    /// <summary>Builds an anonymous client that stops at a redirect instead of following it.</summary>
    /// <param name="host">The host to talk to.</param>
    /// <returns>The client.</returns>
    private static HttpClient NoFollowClient(WebApplicationFactory<Program> host) =>
        host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>Reads the one recipient of a campaign as stored.</summary>
    /// <param name="campaignId">The campaign.</param>
    /// <returns>The stored recipient.</returns>
    private async Task<EmailCampaignRecipient> RecipientAsync(int campaignId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>()
            .EmailCampaignRecipients.AsNoTracking().SingleAsync(r => r.CampaignId == campaignId);
    }

    /// <summary>A valid token and index redirect to the link the body holds at that position, with no cache, and the click is counted.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Click_ValidTokenAndIndex_RedirectsToTheBodyLinkAndCountsTheClick()
    {
        using var scope = factory.Services.CreateScope();
        var seeded = await CampaignTrackingTestSeeds.SeedAsync(scope.ServiceProvider, body: Body);

        var response = await NoFollowClient(factory).GetAsync(Route(seeded.TrackingToken, 0));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be("https://shop.example/offer?a=1&b=2");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var recipient = await RecipientAsync(seeded.CampaignId);
        recipient.ClickCount.Should().Be(1);
        recipient.FirstClickedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5));
        recipient.OpenCount.Should().Be(0);
    }

    /// <summary>The index counts only the trackable links: the mail link and the script link before the second web link do not take a place.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Click_SecondIndex_SkipsTheLinksThatAreNotTracked()
    {
        using var scope = factory.Services.CreateScope();
        var seeded = await CampaignTrackingTestSeeds.SeedAsync(scope.ServiceProvider, body: Body);

        var response = await NoFollowClient(factory).GetAsync(Route(seeded.TrackingToken, 1));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be("http://shop.example/other");
    }

    /// <summary>
    /// No open redirect: a destination offered in the query string, or in a header, is ignored. The only address the endpoint
    /// ever answers is the one in the stored body.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Click_WithADestinationInTheQuery_IgnoresIt()
    {
        using var scope = factory.Services.CreateScope();
        var seeded = await CampaignTrackingTestSeeds.SeedAsync(scope.ServiceProvider, body: Body);

        var response = await NoFollowClient(factory).GetAsync(
            $"{Route(seeded.TrackingToken, 1)}?url=https://evil.example&redirect=https://evil.example&returnUrl=//evil.example&to=https://evil.example");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be("http://shop.example/other");
    }

    /// <summary>An index past the last trackable link is refused with 404 and counts nothing: the endpoint never invents a destination.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Click_IndexBeyondTheLinks_RefusesNotFound()
    {
        using var scope = factory.Services.CreateScope();
        var seeded = await CampaignTrackingTestSeeds.SeedAsync(scope.ServiceProvider, body: Body);

        var response = await NoFollowClient(factory).GetAsync(Route(seeded.TrackingToken, 2));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CAMPAIGN_MAIL_LINK_NOT_FOUND);
        (await RecipientAsync(seeded.CampaignId)).ClickCount.Should().Be(0);
    }

    /// <summary>
    /// A body whose only links are unsafe (script, data, mail, relative, scheme-relative) has nothing to redirect to, whatever
    /// index is asked for.
    /// </summary>
    /// <param name="index">The index to ask for.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public async Task Click_BodyWithOnlyUnsafeLinks_NeverRedirects(int index)
    {
        using var scope = factory.Services.CreateScope();
        var seeded = await CampaignTrackingTestSeeds.SeedAsync(
            scope.ServiceProvider,
            body: "<a href=\"javascript:alert(1)\">a</a><a href=\"data:text/html,x\">b</a><a href=\"mailto:a@example.com\">c</a>"
                + "<a href=\"/local\">d</a><a href=\"//evil.example\">e</a>");

        var response = await NoFollowClient(factory).GetAsync(Route(seeded.TrackingToken, index));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.Location.Should().BeNull();
        (await RecipientAsync(seeded.CampaignId)).ClickCount.Should().Be(0);
    }

    /// <summary>A token that names nobody is refused with 404 and no redirect.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Click_UnknownToken_RefusesNotFound()
    {
        var response = await NoFollowClient(factory).GetAsync(Route(new string('a', 64), 0));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.Location.Should().BeNull();
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CAMPAIGN_MAIL_LINK_NOT_FOUND);
    }

    /// <summary>A negative index or an oversized token is refused with 400 before any lookup.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Click_NegativeIndexOrOversizedToken_RefusesValidation()
    {
        var anonymous = NoFollowClient(factory);

        var negative = await anonymous.GetAsync(Route(new string('a', 64), -1));
        var oversized = await anonymous.GetAsync(Route(new string('a', 65), 0));

        negative.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        oversized.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>An index that is not a number does not match the route at all.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Click_IndexThatIsNotANumber_DoesNotMatchTheRoute()
    {
        var response = await NoFollowClient(factory).GetAsync($"/api/public/campaign-mail/click/{new string('a', 64)}/abc");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.Location.Should().BeNull();
    }

    /// <summary>
    /// End to end: the tracked link in a delivered mail, followed as it stands, leads to the link the admin wrote and is counted
    /// on the recipient it was built for.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Click_TheTrackedLinkOfADeliveredMail_LeadsToTheOriginalLink()
    {
        var sender = new FakeEmailSender();
        using var host = EventHandlerTestHost.WithFakeRelay(factory, sender);
        using var scope = host.Services.CreateScope();
        var seeded = await CampaignTrackingTestSeeds.SeedAsync(scope.ServiceProvider, body: Body);
        await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync<Result>(new DeliverEmailCampaignCommand(seeded.CampaignId), CancellationToken.None);
        var mail = sender.Sent.Single(m => m.To == seeded.Email);
        var path = Regex.Match(mail.Body, "href=\"[^\"]*?(/api/public/campaign-mail/click/[0-9a-f]{64}/1)\"").Groups[1].Value;

        var response = await NoFollowClient(host).GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be("http://shop.example/other");
        (await RecipientAsync(seeded.CampaignId)).ClickCount.Should().Be(1);
    }

    /// <summary>The unsubscribe token is not a tracking token: a tracked-link address built from it finds nobody, redirects nowhere and counts nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Click_WithTheUnsubscribeToken_IsRefusedNotFound()
    {
        using var scope = factory.Services.CreateScope();
        var seeded = await CampaignTrackingTestSeeds.SeedAsync(scope.ServiceProvider, body: Body);

        var response = await NoFollowClient(factory).GetAsync(Route(seeded.UnsubscribeToken, 0));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.Location.Should().BeNull();
        (await RecipientAsync(seeded.CampaignId)).ClickCount.Should().Be(0);
    }
}
