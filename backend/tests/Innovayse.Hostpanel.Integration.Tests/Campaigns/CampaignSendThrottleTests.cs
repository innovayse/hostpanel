using Innovayse.Hostpanel.Application.Campaigns.Commands.DeliverEmailCampaign;
using Innovayse.Hostpanel.Application.Campaigns.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Campaigns.Enums;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>
/// Integration tests for the campaign send rate: <see cref="CampaignSendThrottle"/> answers how many mails the last minute
/// still leaves, and <c>DeliverEmailCampaign</c> sends no more than that, leaving the rest pending for a later pass without
/// sending anyone twice.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CampaignSendThrottleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Writes the stored send rate straight into its row, past the admin validation, and returns the value it replaces.</summary>
    /// <param name="services">The service provider of the host the test drives.</param>
    /// <param name="value">The raw value to store.</param>
    /// <returns>The value the row held before.</returns>
    private static async Task<string> SetRateAsync(IServiceProvider services, string value)
    {
        var context = services.GetRequiredService<IHostpanelDbContext>();
        var row = await context.Settings.SingleAsync(s => s.Key == CampaignSettingKeys.SendRatePerMinute);
        var previous = row.Value;
        row.UpdateValue(value);
        await context.SaveChangesAsync(CancellationToken.None);
        return previous;
    }

    /// <summary>Sends the delivery command for a campaign, as the dispatch job does.</summary>
    /// <param name="services">The service provider of the host the test drives.</param>
    /// <param name="campaignId">The campaign to deliver.</param>
    /// <returns>The command's outcome.</returns>
    private static Task<Result> DeliverAsync(IServiceProvider services, int campaignId) =>
        services.GetRequiredService<IMessageBus>().InvokeAsync<Result>(new DeliverEmailCampaignCommand(campaignId), CancellationToken.None);

    /// <summary>Moves every delivery of a campaign two minutes into the past, as if a minute and more had gone by.</summary>
    /// <param name="services">The service provider of the host the test drives.</param>
    /// <param name="campaignId">The campaign whose deliveries to age.</param>
    /// <returns>A task that completes once the deliveries are aged.</returns>
    private static Task AgeDeliveriesAsync(IServiceProvider services, int campaignId) =>
        services.GetRequiredService<AppDbContext>().Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"EmailCampaignRecipients\" SET \"SentAt\" = \"SentAt\" - INTERVAL '2 minutes' WHERE \"CampaignId\" = {campaignId}");

    /// <summary>
    /// Moves every delivery in the database out of the current minute. The tests of this class share one database, so each
    /// starts from an empty window instead of counting the mails a test before it sent.
    /// </summary>
    /// <param name="services">The service provider of the host the test drives.</param>
    /// <returns>A task that completes once no delivery is inside the window.</returns>
    private static Task ClearWindowAsync(IServiceProvider services) =>
        services.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(
            "UPDATE \"EmailCampaignRecipients\" SET \"SentAt\" = \"SentAt\" - INTERVAL '2 minutes' WHERE \"SentAt\" IS NOT NULL");

    /// <summary>
    /// A pass sends only as many mails as the rate allows and leaves the campaign sending; a pass in the same minute sends
    /// nobody, and once the minute has gone by the rest go out, each recipient exactly once, and the campaign is finished.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Deliver_MoreRecipientsThanTheRate_SendsTheAllowedAndTheRestInTheNextMinute()
    {
        var sender = new FakeEmailSender();
        using var scope = EventHandlerTestHost.WithFakeRelay(factory, sender).Services.CreateScope();
        await ClearWindowAsync(scope.ServiceProvider);
        var seeded = await CampaignTestSeeds.SeedSendingRecipientAsync(scope.ServiceProvider);
        await CampaignTestSeeds.AddRecipientAsync(scope.ServiceProvider, seeded.CampaignId);
        await CampaignTestSeeds.AddRecipientAsync(scope.ServiceProvider, seeded.CampaignId);
        var previous = await SetRateAsync(scope.ServiceProvider, "2");
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        try
        {
            (await DeliverAsync(scope.ServiceProvider, seeded.CampaignId)).IsSuccess.Should().BeTrue();

            sender.Sent.Should().HaveCount(2);
            (await context.EmailCampaignRecipients.AsNoTracking().CountAsync(r => r.CampaignId == seeded.CampaignId && r.Status == CampaignRecipientStatus.Pending))
                .Should().Be(1);
            (await context.EmailCampaigns.AsNoTracking().SingleAsync(c => c.Id == seeded.CampaignId)).Status.Should().Be(CampaignStatus.Sending);

            (await DeliverAsync(scope.ServiceProvider, seeded.CampaignId)).IsSuccess.Should().BeTrue();

            sender.Sent.Should().HaveCount(2, "the minute is used up");

            await AgeDeliveriesAsync(scope.ServiceProvider, seeded.CampaignId);
            (await DeliverAsync(scope.ServiceProvider, seeded.CampaignId)).IsSuccess.Should().BeTrue();

            sender.Sent.Should().HaveCount(3);
            sender.Sent.Select(m => m.To).Distinct().Should().HaveCount(3, "nobody is mailed twice");
            (await context.EmailCampaigns.AsNoTracking().SingleAsync(c => c.Id == seeded.CampaignId)).Status.Should().Be(CampaignStatus.Sent);
        }
        finally
        {
            await SetRateAsync(scope.ServiceProvider, previous);
        }
    }

    /// <summary>The remaining allowance is the rate less the deliveries of the last minute over every campaign.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Remaining_CountsTheDeliveriesOfEveryCampaignInTheLastMinute()
    {
        var sender = new FakeEmailSender();
        using var scope = EventHandlerTestHost.WithFakeRelay(factory, sender).Services.CreateScope();
        await ClearWindowAsync(scope.ServiceProvider);
        var first = await CampaignTestSeeds.SeedSendingRecipientAsync(scope.ServiceProvider);
        var second = await CampaignTestSeeds.SeedSendingRecipientAsync(scope.ServiceProvider);
        await DeliverAsync(scope.ServiceProvider, first.CampaignId);
        await DeliverAsync(scope.ServiceProvider, second.CampaignId);
        var previous = await SetRateAsync(scope.ServiceProvider, "5");
        try
        {
            var throttle = scope.ServiceProvider.GetRequiredService<CampaignSendThrottle>();

            (await throttle.RemainingAsync(CancellationToken.None)).Should().Be(3);

            await AgeDeliveriesAsync(scope.ServiceProvider, first.CampaignId);
            (await throttle.RemainingAsync(CancellationToken.None)).Should().Be(4, "a delivery older than a minute no longer counts");
        }
        finally
        {
            await SetRateAsync(scope.ServiceProvider, previous);
        }
    }

    /// <summary>The allowance never goes above one job batch, the most a pass reads at a time.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Remaining_IsCappedAtOneJobBatch()
    {
        using var scope = factory.Services.CreateScope();
        await ClearWindowAsync(scope.ServiceProvider);
        var throttle = scope.ServiceProvider.GetRequiredService<CampaignSendThrottle>();
        var previous = await SetRateAsync(scope.ServiceProvider, "600");
        try
        {
            (await throttle.RemainingAsync(CancellationToken.None)).Should().Be(JobBatch.Size);
        }
        finally
        {
            await SetRateAsync(scope.ServiceProvider, previous);
        }
    }

    /// <summary>A stored value that does not match the pattern falls back to the seeded default, so a typo cannot lift the limit.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Remaining_InvalidStoredRate_FallsBackToTheDefault()
    {
        using var scope = factory.Services.CreateScope();
        await ClearWindowAsync(scope.ServiceProvider);
        var throttle = scope.ServiceProvider.GetRequiredService<CampaignSendThrottle>();
        var previous = await SetRateAsync(scope.ServiceProvider, "plenty");
        try
        {
            (await throttle.RemainingAsync(CancellationToken.None)).Should().Be(60);
        }
        finally
        {
            await SetRateAsync(scope.ServiceProvider, previous);
        }
    }
}
