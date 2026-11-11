using System.Net;
using Innovayse.Hostpanel.Application.Campaigns.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Campaigns.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>
/// Integration tests for the one-click unsubscribe (RFC 8058): the address in the <c>List-Unsubscribe</c> header of a campaign
/// mail, posted to by the mail client with the form body <c>List-Unsubscribe=One-Click</c>, with no session and none of the
/// headers the client portal's own server asks for.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class OneClickUnsubscribeTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The public address the link is built on; only its path matters to the test host.</summary>
    private const string PublicBaseUrl = "https://portal.example.test";

    /// <summary>The body a mail client posts to the address, fixed by RFC 8058.</summary>
    /// <returns>The form body.</returns>
    private static FormUrlEncodedContent OneClickBody() =>
        new([new KeyValuePair<string?, string?>("List-Unsubscribe", "One-Click")]);

    /// <summary>Takes the path and query out of the address a mail carries, which is what the test host is called with.</summary>
    /// <param name="token">The recipient's unsubscribe token.</param>
    /// <returns>The path and query of the one-click address.</returns>
    private static string RouteFor(string token) =>
        new Uri(CampaignUnsubscribeLink.OneClickFor(PublicBaseUrl, token)).PathAndQuery;

    /// <summary>The address the mail carries, posted to the way a mail client does, withdraws the consent and marks the recipient.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OneClick_PostToTheMailAddress_WithdrawsConsentAndMarksTheRecipient()
    {
        using var scope = factory.Services.CreateScope();
        var seeded = await CampaignTestSeeds.SeedSendingRecipientAsync(scope.ServiceProvider);

        var response = await factory.CreateClient().PostAsync(RouteFor(seeded.Token), OneClickBody());

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var check = factory.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Clients.AsNoTracking().SingleAsync(c => c.Id == seeded.ClientId)).MarketingOptIn.Should().BeFalse();
        (await context.EmailCampaignRecipients.AsNoTracking().SingleAsync(r => r.CampaignId == seeded.CampaignId))
            .Status.Should().Be(CampaignRecipientStatus.Unsubscribed);
    }

    /// <summary>A mail client may post the same address twice; the second answer is the same success.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OneClick_PostedTwice_StaysSuccessful()
    {
        using var scope = factory.Services.CreateScope();
        var seeded = await CampaignTestSeeds.SeedSendingRecipientAsync(scope.ServiceProvider);
        var anonymous = factory.CreateClient();

        await anonymous.PostAsync(RouteFor(seeded.Token), OneClickBody());
        var again = await anonymous.PostAsync(RouteFor(seeded.Token), OneClickBody());

        again.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>A token that names nobody is refused with 404 and <c>CAMPAIGN_UNSUBSCRIBE_LINK_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OneClick_UnknownToken_RefusesNotFound()
    {
        var response = await factory.CreateClient().PostAsync(RouteFor(new string('a', 64)), OneClickBody());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CAMPAIGN_UNSUBSCRIBE_LINK_NOT_FOUND);
    }

    /// <summary>An address without a token is refused with 400 before any lookup.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OneClick_MissingToken_RefusesValidation()
    {
        var response = await factory.CreateClient().PostAsync(CampaignUnsubscribeLink.OneClickPath, OneClickBody());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A token longer than any real one is refused with 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OneClick_OversizedToken_RefusesValidation()
    {
        var response = await factory.CreateClient().PostAsync(RouteFor(new string('a', 65)), OneClickBody());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A browser that opens the address (a GET) changes nothing: only a POST unsubscribes, as RFC 8058 requires.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OneClick_Get_DoesNotUnsubscribe()
    {
        using var scope = factory.Services.CreateScope();
        var seeded = await CampaignTestSeeds.SeedSendingRecipientAsync(scope.ServiceProvider);

        var response = await factory.CreateClient().GetAsync(RouteFor(seeded.Token));

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        using var check = factory.Services.CreateScope();
        (await check.ServiceProvider.GetRequiredService<IHostpanelDbContext>().Clients.AsNoTracking()
            .SingleAsync(c => c.Id == seeded.ClientId)).MarketingOptIn.Should().BeTrue();
    }
}
