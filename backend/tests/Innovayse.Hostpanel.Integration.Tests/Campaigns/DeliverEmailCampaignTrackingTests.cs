using System.Text.RegularExpressions;
using Innovayse.Hostpanel.Application.Campaigns.Commands.DeliverEmailCampaign;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Campaigns;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>
/// Integration tests for what <c>DeliverEmailCampaign</c> adds to a mail for open and click tracking: each recipient's own
/// tracked links and open pixel, built from a tracking token of their own that is not the one in its unsubscribe link, with the stored body left as the
/// admin wrote it and both tokens kept out of the email log.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeliverEmailCampaignTrackingTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A body with two web links, a mail link and an unsafe link.</summary>
    private const string Body =
        "<p><a href=\"https://shop.example/offer?a=1&amp;b=2\">Offer</a> <a href=\"mailto:sales@example.com\">Write</a> "
        + "<a href=\"javascript:alert(1)\">Nope</a> <a href=\"http://shop.example/other\">Other</a></p>";

    /// <summary>Sends the delivery command for a campaign.</summary>
    /// <param name="services">The host's service provider.</param>
    /// <param name="campaignId">The campaign to deliver.</param>
    /// <returns>A task that completes once the batch was handled.</returns>
    private static Task<Result> DeliverAsync(IServiceProvider services, int campaignId) =>
        services.GetRequiredService<IMessageBus>().InvokeAsync<Result>(new DeliverEmailCampaignCommand(campaignId), CancellationToken.None);

    /// <summary>The web links are pointed at the click endpoint under the recipient's token, the other links are left, and the pixel ends the body.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Deliver_Mail_CarriesTrackedLinksAndThePixel()
    {
        var sender = new FakeEmailSender();
        using var scope = EventHandlerTestHost.WithFakeRelay(factory, sender).Services.CreateScope();
        var seeded = await CampaignTestSeeds.SeedSendingRecipientAsync(scope.ServiceProvider, body: Body);

        await DeliverAsync(scope.ServiceProvider, seeded.CampaignId);

        var mail = sender.Sent.Single(m => m.To == seeded.Email);
        var token = Regex.Match(mail.Body, "/api/public/campaign-mail/open/([0-9a-f]{64})\"").Groups[1].Value;
        token.Should().HaveLength(64);
        var unsubscribeToken = Regex.Match(mail.Body, @"/unsubscribe\?token=([0-9a-f]{64})").Groups[1].Value;
        mail.Body.Should()
            .Contain($"/api/public/campaign-mail/click/{token}/0\">Offer")
            .And.Contain($"/api/public/campaign-mail/click/{token}/1\">Other")
            .And.Contain("href=\"mailto:sales@example.com\"")
            .And.Contain("href=\"javascript:alert(1)\"")
            .And.NotContain("shop.example")
            .And.Contain($"/unsubscribe?token={unsubscribeToken}");
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var recipient = await context.EmailCampaignRecipients.AsNoTracking().SingleAsync(r => r.CampaignId == seeded.CampaignId);
        recipient.TrackingTokenHash.Should().Be(EmailCampaignRecipient.HashToken(token));
        recipient.UnsubscribeTokenHash.Should().Be(EmailCampaignRecipient.HashToken(unsubscribeToken));
        unsubscribeToken.Should().NotBe(token);
    }

    /// <summary>The campaign keeps the body the admin wrote; only the mail is rewritten, so a click is answered from the original links.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Deliver_Mail_LeavesTheStoredBodyAsWritten()
    {
        var sender = new FakeEmailSender();
        using var scope = EventHandlerTestHost.WithFakeRelay(factory, sender).Services.CreateScope();
        var seeded = await CampaignTestSeeds.SeedSendingRecipientAsync(scope.ServiceProvider, body: Body);

        await DeliverAsync(scope.ServiceProvider, seeded.CampaignId);

        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.EmailCampaigns.AsNoTracking().SingleAsync(c => c.Id == seeded.CampaignId)).Body.Should().Be(Body);
    }

    /// <summary>The email log keeps no trace of either token, neither in the unsubscribe link nor in the tracked addresses.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Deliver_Mail_IsLoggedWithoutTheTokenInTheTrackedAddresses()
    {
        var sender = new FakeEmailSender();
        using var scope = EventHandlerTestHost.WithFakeRelay(factory, sender).Services.CreateScope();
        var seeded = await CampaignTestSeeds.SeedSendingRecipientAsync(scope.ServiceProvider, body: Body);

        await DeliverAsync(scope.ServiceProvider, seeded.CampaignId);

        var body = sender.Sent.Single(m => m.To == seeded.Email).Body;
        var token = Regex.Match(body, "/open/([0-9a-f]{64})").Groups[1].Value;
        var unsubscribeToken = Regex.Match(body, @"/unsubscribe\?token=([0-9a-f]{64})").Groups[1].Value;
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var logged = await context.EmailLogs.AsNoTracking().SingleAsync(l => l.To == seeded.Email);
        logged.Body.Should().NotContain(token).And.NotContain(unsubscribeToken).And.Contain("/api/public/campaign-mail/click/").And.Contain("/api/public/campaign-mail/open/");
    }
}
