using System.Text.RegularExpressions;
using Innovayse.Hostpanel.Application.Campaigns.Commands.DeliverEmailCampaign;
using Innovayse.Hostpanel.Application.Campaigns.Commands.UnsubscribeFromEmailCampaigns;
using Innovayse.Hostpanel.Application.Campaigns.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Campaigns;
using Innovayse.Hostpanel.Domain.Campaigns.Enums;
using Innovayse.Hostpanel.Domain.Notifications;
using Innovayse.Hostpanel.Domain.Notifications.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>
/// Integration tests for the <c>DeliverEmailCampaign</c> use case, driven through the bus the way the dispatch job does:
/// the mail reaches the recipient in the campaign template with a personal unsubscribe link, the outcome lands on the
/// recipient, and the campaign is finished once nobody is waiting.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeliverEmailCampaignTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Sends the delivery command for a campaign on a host with the given relay.</summary>
    /// <param name="services">The host's service provider.</param>
    /// <param name="campaignId">The campaign to deliver.</param>
    /// <returns>The command's outcome.</returns>
    private static Task<Result> DeliverAsync(IServiceProvider services, int campaignId) =>
        services.GetRequiredService<IMessageBus>().InvokeAsync<Result>(new DeliverEmailCampaignCommand(campaignId), CancellationToken.None);

    /// <summary>Reads the unsubscribe token out of the link in a delivered mail.</summary>
    /// <param name="mail">The mail the relay was handed.</param>
    /// <returns>The token the link carries.</returns>
    private static string TokenOf(EmailMessage mail) =>
        Regex.Match(mail.Body, @"/unsubscribe\?token=([0-9a-f]{64})").Groups[1].Value;

    /// <summary>A pending recipient is mailed the campaign body with its own unsubscribe link, and the campaign is finished.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Deliver_PendingRecipient_IsMailedWithItsUnsubscribeLink()
    {
        var sender = new FakeEmailSender();
        using var scope = EventHandlerTestHost.WithFakeRelay(factory, sender).Services.CreateScope();
        var seeded = await CampaignTestSeeds.SeedSendingRecipientAsync(scope.ServiceProvider, body: "<p>Autumn <b>sale</b></p>");

        var result = await DeliverAsync(scope.ServiceProvider, seeded.CampaignId);

        result.IsSuccess.Should().BeTrue();
        var mail = sender.Sent.Where(m => m.To == seeded.Email).Should().ContainSingle().Which;
        mail.Subject.Should().Be("Autumn offer");
        var token = TokenOf(mail);
        mail.Body.Should().Contain("<p>Autumn <b>sale</b></p>").And.Contain($"/unsubscribe?token={token}");
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var recipient = await context.EmailCampaignRecipients.AsNoTracking().SingleAsync(r => r.CampaignId == seeded.CampaignId);
        recipient.UnsubscribeTokenHash.Should().Be(EmailCampaignRecipient.HashToken(token));
        recipient.Status.Should().Be(CampaignRecipientStatus.Sent);
        recipient.SentAt.Should().NotBeNull();
        (await context.EmailCampaigns.AsNoTracking().SingleAsync(c => c.Id == seeded.CampaignId)).Status.Should().Be(CampaignStatus.Sent);
    }

    /// <summary>Delivering again after the campaign is sent mails nobody a second time.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Deliver_Twice_SendsOnce()
    {
        var sender = new FakeEmailSender();
        using var scope = EventHandlerTestHost.WithFakeRelay(factory, sender).Services.CreateScope();
        var seeded = await CampaignTestSeeds.SeedSendingRecipientAsync(scope.ServiceProvider);

        await DeliverAsync(scope.ServiceProvider, seeded.CampaignId);
        var again = await DeliverAsync(scope.ServiceProvider, seeded.CampaignId);

        again.IsSuccess.Should().BeTrue();
        sender.Sent.Where(m => m.To == seeded.Email).Should().ContainSingle();
    }

    /// <summary>A relay that refuses the mail leaves the recipient pending until every attempt is used, then failed with the reason.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Deliver_RefusedByTheRelay_RetriesThenMarksFailed()
    {
        var sender = new FakeEmailSender { FailWith = EmailSendFailure.RecipientRejected };
        using var scope = EventHandlerTestHost.WithFakeRelay(factory, sender).Services.CreateScope();
        var seeded = await CampaignTestSeeds.SeedSendingRecipientAsync(scope.ServiceProvider);
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        await DeliverAsync(scope.ServiceProvider, seeded.CampaignId);

        var first = await context.EmailCampaignRecipients.AsNoTracking().SingleAsync(r => r.CampaignId == seeded.CampaignId);
        first.Status.Should().Be(CampaignRecipientStatus.Pending);
        first.Attempts.Should().Be(1);
        (await context.EmailCampaigns.AsNoTracking().SingleAsync(c => c.Id == seeded.CampaignId)).Status.Should().Be(CampaignStatus.Sending);

        await DeliverAsync(scope.ServiceProvider, seeded.CampaignId);
        await DeliverAsync(scope.ServiceProvider, seeded.CampaignId);

        var last = await context.EmailCampaignRecipients.AsNoTracking().SingleAsync(r => r.CampaignId == seeded.CampaignId);
        last.Status.Should().Be(CampaignRecipientStatus.Failed);
        last.FailureReason.Should().Be(nameof(EmailSendFailure.RecipientRejected));
        (await context.EmailCampaigns.AsNoTracking().SingleAsync(c => c.Id == seeded.CampaignId)).Status.Should().Be(CampaignStatus.Sent);
    }

    /// <summary>A client who withdrew consent after the recipients were fixed is skipped and counted as unsubscribed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Deliver_ClientWithoutConsent_IsSkipped()
    {
        var sender = new FakeEmailSender();
        using var scope = EventHandlerTestHost.WithFakeRelay(factory, sender).Services.CreateScope();
        var seeded = await CampaignTestSeeds.SeedSendingRecipientAsync(scope.ServiceProvider, optedIn: false);

        await DeliverAsync(scope.ServiceProvider, seeded.CampaignId);

        sender.Sent.Where(m => m.To == seeded.Email).Should().BeEmpty();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var recipient = await context.EmailCampaignRecipients.AsNoTracking().SingleAsync(r => r.CampaignId == seeded.CampaignId);
        recipient.Status.Should().Be(CampaignRecipientStatus.Unsubscribed);
        (await context.EmailCampaigns.AsNoTracking().SingleAsync(c => c.Id == seeded.CampaignId)).Status.Should().Be(CampaignStatus.Sent);
    }

    /// <summary>The mail is handed to the relay with the one-click endpoint as its list-unsubscribe address, so a mail client can unsubscribe the reader with a single POST.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Deliver_Mail_CarriesTheUnsubscribeAddressForTheRelay()
    {
        var sender = new FakeEmailSender();
        using var scope = EventHandlerTestHost.WithFakeRelay(factory, sender).Services.CreateScope();
        var seeded = await CampaignTestSeeds.SeedSendingRecipientAsync(scope.ServiceProvider);

        await DeliverAsync(scope.ServiceProvider, seeded.CampaignId);

        var mail = sender.Sent.Where(m => m.To == seeded.Email).Should().ContainSingle().Which;
        mail.UnsubscribeUrl.Should().EndWith($"{CampaignUnsubscribeLink.OneClickPath}?token={TokenOf(mail)}");
    }

    /// <summary>The email log keeps the mail without the personal token, so nobody who reads the log can follow a client's link.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Deliver_Mail_IsLoggedWithoutTheToken()
    {
        var sender = new FakeEmailSender();
        using var scope = EventHandlerTestHost.WithFakeRelay(factory, sender).Services.CreateScope();
        var seeded = await CampaignTestSeeds.SeedSendingRecipientAsync(scope.ServiceProvider, body: "<p>Autumn sale</p>");

        await DeliverAsync(scope.ServiceProvider, seeded.CampaignId);

        var token = TokenOf(sender.Sent.Single(m => m.To == seeded.Email));
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var logged = await context.EmailLogs.AsNoTracking().SingleAsync(l => l.To == seeded.Email);
        logged.Body.Should().Contain("<p>Autumn sale</p>").And.Contain("href=\"***\"").And.NotContain(token);
    }

    /// <summary>
    /// After a crash that lost the outcomes, the next pass sends again only the mails the log does not show as delivered:
    /// the recipient whose mail went out is marked sent without a second mail, the other one is mailed.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Deliver_AfterACrash_MailsOnlyWhatTheLogDoesNotShowDelivered()
    {
        var sender = new FakeEmailSender();
        using var host = EventHandlerTestHost.WithFakeRelay(factory, sender);
        using var seedScope = host.Services.CreateScope();
        var seeded = await CampaignTestSeeds.SeedSendingRecipientAsync(seedScope.ServiceProvider);
        var lostEmail = await CampaignTestSeeds.AddRecipientAsync(seedScope.ServiceProvider, seeded.CampaignId);
        using (var first = host.Services.CreateScope())
        {
            await DeliverAsync(first.ServiceProvider, seeded.CampaignId);
        }

        // The crash: both outcomes were lost, and the second mail's log line with them (the relay never got it).
        using (var crash = host.Services.CreateScope())
        {
            var db = crash.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"EmailCampaignRecipients\" SET \"Status\" = 'Pending', \"Attempts\" = 0, \"SentAt\" = NULL WHERE \"CampaignId\" = {seeded.CampaignId}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"EmailCampaigns\" SET \"Status\" = 'Sending', \"SentAt\" = NULL WHERE \"Id\" = {seeded.CampaignId}");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"EmailLogs\" WHERE \"To\" = {lostEmail}");
        }

        using (var resume = host.Services.CreateScope())
        {
            var result = await DeliverAsync(resume.ServiceProvider, seeded.CampaignId);

            result.IsSuccess.Should().BeTrue();
        }

        sender.Sent.Count(m => m.To == seeded.Email).Should().Be(1);
        sender.Sent.Count(m => m.To == lostEmail).Should().Be(2);
        using var check = host.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.EmailCampaignRecipients.AsNoTracking().Where(r => r.CampaignId == seeded.CampaignId).Select(r => r.Status).ToListAsync())
            .Should().OnlyContain(status => status == CampaignRecipientStatus.Sent);
        (await context.EmailCampaigns.AsNoTracking().SingleAsync(c => c.Id == seeded.CampaignId)).Status.Should().Be(CampaignStatus.Sent);
    }

    /// <summary>
    /// A crash lost the outcome of a mail the relay had already accepted. The resumed pass finds the delivered log line, marks the
    /// recipient sent and sends nothing, and the token hash stays the one of the mail in the mailbox, so its link still works.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Deliver_ResumedAfterTheMailWasAccepted_KeepsTheOriginalTokenValid()
    {
        var sender = new FakeEmailSender();
        using var host = EventHandlerTestHost.WithFakeRelay(factory, sender);
        using var seedScope = host.Services.CreateScope();
        var seeded = await CampaignTestSeeds.SeedSendingRecipientAsync(seedScope.ServiceProvider);
        using (var first = host.Services.CreateScope())
        {
            await DeliverAsync(first.ServiceProvider, seeded.CampaignId);
        }

        var token = TokenOf(sender.Sent.Single(m => m.To == seeded.Email));
        using (var crash = host.Services.CreateScope())
        {
            var db = crash.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"EmailCampaignRecipients\" SET \"Status\" = 'Pending', \"Attempts\" = 0, \"SentAt\" = NULL WHERE \"CampaignId\" = {seeded.CampaignId}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"EmailCampaigns\" SET \"Status\" = 'Sending', \"SentAt\" = NULL WHERE \"Id\" = {seeded.CampaignId}");
        }

        using (var resume = host.Services.CreateScope())
        {
            (await DeliverAsync(resume.ServiceProvider, seeded.CampaignId)).IsSuccess.Should().BeTrue();
        }

        sender.Sent.Where(m => m.To == seeded.Email).Should().ContainSingle();
        using var check = host.Services.CreateScope();
        var recipient = await check.ServiceProvider.GetRequiredService<IHostpanelDbContext>().EmailCampaignRecipients.AsNoTracking()
            .SingleAsync(r => r.CampaignId == seeded.CampaignId);
        recipient.Status.Should().Be(CampaignRecipientStatus.Sent);
        recipient.UnsubscribeTokenHash.Should().Be(EmailCampaignRecipient.HashToken(token));
        (await check.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync<Result>(new UnsubscribeFromEmailCampaignsCommand(token), CancellationToken.None)).IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// A reader follows the unsubscribe link while the mail is leaving. The unsubscribe is saved first, so the sender's save of
    /// the same recipient conflicts: nothing fails, the recipient stays unsubscribed, the consent is withdrawn, and the next
    /// pass finishes the campaign.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Deliver_ReaderUnsubscribesWhileTheMailLeaves_EndsUnsubscribedWithoutAConflict()
    {
        WebApplicationFactory<Program> host = null!;
        var sender = new HookedEmailSender(async mail =>
        {
            using var reader = host.Services.CreateScope();
            var outcome = await reader.ServiceProvider.GetRequiredService<IMessageBus>()
                .InvokeAsync<Result>(new UnsubscribeFromEmailCampaignsCommand(TokenOf(mail)), CancellationToken.None);
            outcome.IsSuccess.Should().BeTrue();
        });
        host = EventHandlerTestHost.WithFakeRelay(factory, sender);
        using var scope = host.Services.CreateScope();
        var seeded = await CampaignTestSeeds.SeedSendingRecipientAsync(scope.ServiceProvider);

        var result = await DeliverAsync(scope.ServiceProvider, seeded.CampaignId);

        result.IsSuccess.Should().BeTrue();
        using var check = host.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.EmailCampaignRecipients.AsNoTracking().SingleAsync(r => r.CampaignId == seeded.CampaignId)).Status
            .Should().Be(CampaignRecipientStatus.Unsubscribed);
        (await context.Clients.AsNoTracking().SingleAsync(c => c.Id == seeded.ClientId)).MarketingOptIn.Should().BeFalse();

        using var next = host.Services.CreateScope();
        await DeliverAsync(next.ServiceProvider, seeded.CampaignId);
        (await next.ServiceProvider.GetRequiredService<IHostpanelDbContext>().EmailCampaigns.AsNoTracking().SingleAsync(c => c.Id == seeded.CampaignId))
            .Status.Should().Be(CampaignStatus.Sent);
        host.Dispose();
    }

    /// <summary>A campaign that is not sending is left alone and the command still succeeds.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Deliver_DraftCampaign_DoesNothing()
    {
        var sender = new FakeEmailSender();
        using var scope = EventHandlerTestHost.WithFakeRelay(factory, sender).Services.CreateScope();
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await CampaignTestSeeds.CreateAsync(client);

        var result = await DeliverAsync(scope.ServiceProvider, id);

        result.IsSuccess.Should().BeTrue();
        sender.Sent.Should().BeEmpty();
    }

    /// <summary>An id that names no campaign is refused with <c>CAMPAIGN_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Deliver_UnknownCampaign_RefusesNotFound()
    {
        using var scope = factory.Services.CreateScope();

        var result = await DeliverAsync(scope.ServiceProvider, 999_999);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.CAMPAIGN_NOT_FOUND);
    }
}
