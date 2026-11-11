using Innovayse.Hostpanel.Application.Campaigns.Commands.StartEmailCampaign;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Campaigns.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>
/// Integration tests for the <c>StartEmailCampaign</c> use case, driven through the bus the way the dispatch job does:
/// the audience is resolved once into pending recipients, one per mailbox, and the campaign moves to sending.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class StartEmailCampaignTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Sends the start command for a campaign.</summary>
    /// <param name="services">The host's service provider.</param>
    /// <param name="campaignId">The campaign to start.</param>
    /// <returns>The command's outcome.</returns>
    private static Task<Result> StartAsync(IServiceProvider services, int campaignId) =>
        services.GetRequiredService<IMessageBus>().InvokeAsync<Result>(new StartEmailCampaignCommand(campaignId), CancellationToken.None);

    /// <summary>The recipients are the opted-in owners of the product, each pending with an address and no token yet (the token is issued when the mail is sent).</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_ScheduledCampaign_FixesTheAudienceAsPendingRecipients()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        using var scope = factory.Services.CreateScope();
        var owner = await CampaignTestSeeds.SeedAudienceClientAsync(scope.ServiceProvider);
        var optedOut = await CampaignTestSeeds.SeedAudienceClientAsync(scope.ServiceProvider, optedIn: false);
        var other = await CampaignTestSeeds.SeedAudienceClientAsync(scope.ServiceProvider);
        var productId = await CampaignTestSeeds.SeedServiceAsync(scope.ServiceProvider, owner.Client.Id);
        await CampaignTestSeeds.SeedServiceAsync(scope.ServiceProvider, optedOut.Client.Id, productId);
        await CampaignTestSeeds.SeedServiceAsync(scope.ServiceProvider, other.Client.Id);
        var id = await CampaignTestSeeds.CreateAsync(admin, [new { filterType = "Product", filterValue = productId.ToString() }]);
        await CampaignTestSeeds.ScheduleAsync(admin, id);

        var result = await StartAsync(scope.ServiceProvider, id);

        result.IsSuccess.Should().BeTrue();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var recipient = await context.EmailCampaignRecipients.AsNoTracking().Where(r => r.CampaignId == id).SingleAsync();
        recipient.ClientId.Should().Be(owner.Client.Id);
        recipient.Email.Should().Be(owner.Email);
        recipient.Status.Should().Be(CampaignRecipientStatus.Pending);
        recipient.UnsubscribeTokenHash.Should().BeNull();
        (await context.EmailCampaigns.AsNoTracking().SingleAsync(c => c.Id == id)).Status.Should().Be(CampaignStatus.Sending);
    }

    /// <summary>A client whose account is not active is not a recipient, even when it owns the product and agreed to marketing email.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_NonActiveClient_IsNotARecipient()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        using var scope = factory.Services.CreateScope();
        var active = await CampaignTestSeeds.SeedAudienceClientAsync(scope.ServiceProvider);
        var suspended = await CampaignTestSeeds.SeedSuspendedAudienceClientAsync(scope.ServiceProvider);
        var productId = await CampaignTestSeeds.SeedServiceAsync(scope.ServiceProvider, active.Client.Id);
        await CampaignTestSeeds.SeedServiceAsync(scope.ServiceProvider, suspended.Client.Id, productId);
        var id = await CampaignTestSeeds.CreateAsync(admin, [new { filterType = "Product", filterValue = productId.ToString() }]);
        await CampaignTestSeeds.ScheduleAsync(admin, id);

        await StartAsync(scope.ServiceProvider, id);

        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var recipient = await context.EmailCampaignRecipients.AsNoTracking().Where(r => r.CampaignId == id).SingleAsync();
        recipient.ClientId.Should().Be(active.Client.Id);
    }

    /// <summary>
    /// Two clients whose accounts reach the same mailbox, written in a different case, make one recipient: the one mail goes to
    /// the client with the lowest id.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_ClientsSharingAnAddress_MakeOneRecipient()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        using var scope = factory.Services.CreateScope();
        var first = await CampaignTestSeeds.SeedAudienceClientAsync(scope.ServiceProvider);
        var second = await CampaignTestSeeds.SeedAudienceClientAsync(scope.ServiceProvider);
        await CampaignTestSeeds.ShareAddressAsync(scope.ServiceProvider, second.Client, first.Email);
        var productId = await CampaignTestSeeds.SeedServiceAsync(scope.ServiceProvider, first.Client.Id);
        await CampaignTestSeeds.SeedServiceAsync(scope.ServiceProvider, second.Client.Id, productId);
        var id = await CampaignTestSeeds.CreateAsync(admin, [new { filterType = "Product", filterValue = productId.ToString() }]);
        await CampaignTestSeeds.ScheduleAsync(admin, id);

        await StartAsync(scope.ServiceProvider, id);

        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var recipient = await context.EmailCampaignRecipients.AsNoTracking().Where(r => r.CampaignId == id).SingleAsync();
        recipient.ClientId.Should().Be(first.Client.Id);
        recipient.Email.Should().Be(first.Email);
    }

    /// <summary>A campaign whose audience is empty still starts, with no recipients.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_EmptyAudience_StartsWithoutRecipients()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        using var scope = factory.Services.CreateScope();
        var id = await CampaignTestSeeds.CreateAsync(admin, [new { filterType = "Product", filterValue = "2000000000" }]);
        await CampaignTestSeeds.ScheduleAsync(admin, id);

        var result = await StartAsync(scope.ServiceProvider, id);

        result.IsSuccess.Should().BeTrue();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.EmailCampaignRecipients.AsNoTracking().CountAsync(r => r.CampaignId == id)).Should().Be(0);
        (await context.EmailCampaigns.AsNoTracking().SingleAsync(c => c.Id == id)).Status.Should().Be(CampaignStatus.Sending);
    }

    /// <summary>A draft cannot be started: the domain refuses it as not scheduled.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_DraftCampaign_IsRefusedAsNotScheduled()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        using var scope = factory.Services.CreateScope();
        var id = await CampaignTestSeeds.CreateAsync(admin);

        var start = () => StartAsync(scope.ServiceProvider, id);

        (await start.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be(HostpanelErrorCodes.CAMPAIGN_NOT_SCHEDULED);
    }

    /// <summary>An id that names no campaign is refused with <c>CAMPAIGN_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_UnknownCampaign_RefusesNotFound()
    {
        using var scope = factory.Services.CreateScope();

        var result = await StartAsync(scope.ServiceProvider, 999_999);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.CAMPAIGN_NOT_FOUND);
    }
}
