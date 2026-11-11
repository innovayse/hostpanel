using Innovayse.Hostpanel.Application.Campaigns.Commands.DispatchEmailCampaignsCron;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Campaigns;
using Innovayse.Hostpanel.Domain.Campaigns.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>
/// Integration tests for the <c>DispatchEmailCampaignsCron</c> use case: one pass takes a campaign that is due from
/// scheduled to sent, and leaves a campaign that is not due alone.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DispatchEmailCampaignsCronTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A campaign scheduled for now is started and delivered in one pass, and its recipient is mailed once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Pass_DueCampaign_StartsAndDeliversIt()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender);
        using var scope = host.Services.CreateScope();
        await CampaignTestSeeds.SeedTemplateAsync(scope.ServiceProvider);
        var owner = await CampaignTestSeeds.SeedAudienceClientAsync(scope.ServiceProvider);
        var productId = await CampaignTestSeeds.SeedServiceAsync(scope.ServiceProvider, owner.Client.Id);
        var campaignId = await SeedDueCampaignAsync(scope.ServiceProvider, productId);

        await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync(new DispatchEmailCampaignsCronCommand(), CancellationToken.None);

        sender.Sent.Where(m => m.To == owner.Email).Should().ContainSingle().Which.Body.Should().Contain("/unsubscribe?token=");
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var campaign = await context.EmailCampaigns.AsNoTracking().SingleAsync(c => c.Id == campaignId);
        campaign.Status.Should().Be(CampaignStatus.Sent);
        campaign.SentAt.Should().NotBeNull();
        (await context.EmailCampaignRecipients.AsNoTracking().SingleAsync(r => r.CampaignId == campaignId)).Status
            .Should().Be(CampaignRecipientStatus.Sent);
    }

    /// <summary>A campaign scheduled for later is left scheduled and nobody is mailed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Pass_CampaignNotYetDue_IsLeftAlone()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender);
        using var scope = host.Services.CreateScope();
        var owner = await CampaignTestSeeds.SeedAudienceClientAsync(scope.ServiceProvider);
        var productId = await CampaignTestSeeds.SeedServiceAsync(scope.ServiceProvider, owner.Client.Id);
        var campaignId = await SeedDueCampaignAsync(scope.ServiceProvider, productId, dueInDays: 2);

        await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync(new DispatchEmailCampaignsCronCommand(), CancellationToken.None);

        sender.Sent.Where(m => m.To == owner.Email).Should().BeEmpty();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.EmailCampaigns.AsNoTracking().SingleAsync(c => c.Id == campaignId)).Status.Should().Be(CampaignStatus.Scheduled);
    }

    /// <summary>The pass schedules the next one, so the chain keeps going.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Pass_SchedulesTheNextPass()
    {
        var host = EventHandlerTestHost.WithFakeRelay(factory, new FakeEmailSender());

        var session = await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .InvokeMessageAndWaitAsync(new DispatchEmailCampaignsCronCommand());

        var handled = session.AllRecordsInOrder()
            .First(r => r.MessageEventType == MessageEventType.ExecutionStarted
                        && r.Envelope?.Message?.GetType() == typeof(DispatchEmailCampaignsCronCommand))
            .Envelope!.Id;
        session.Sent.Envelopes()
            .Where(e => e.Message?.GetType() == typeof(DispatchEmailCampaignsCronCommand) && e.Id != handled)
            .Should().NotBeEmpty();
    }

    /// <summary>Writes a campaign for the product that is scheduled to be due the given number of days from now.</summary>
    /// <param name="services">The host's service provider.</param>
    /// <param name="productId">The product whose owners the campaign goes to.</param>
    /// <param name="dueInDays">Days until the campaign is due; zero is due now.</param>
    /// <returns>The campaign's primary key.</returns>
    private static async Task<int> SeedDueCampaignAsync(IServiceProvider services, int productId, int dueInDays = 0)
    {
        var context = services.GetRequiredService<IHostpanelDbContext>();
        var now = services.GetRequiredService<IClock>().UtcNow;
        var campaign = EmailCampaign.Create(
            "Autumn offer", "<p>Hello</p>", "admin-1", [new(CampaignFilterType.Product, productId.ToString())]);
        campaign.Schedule(dueInDays == 0 ? null : now.AddDays(dueInDays), now);
        context.EmailCampaigns.Add(campaign);
        await context.SaveChangesAsync(CancellationToken.None);
        return campaign.Id;
    }
}
