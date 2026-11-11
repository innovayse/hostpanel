using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Campaigns.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>
/// Integration tests for the <c>UnsubscribeFromEmailCampaigns</c> use case: <c>POST /api/public/unsubscribe</c>, the anonymous
/// endpoint behind the link in every campaign mail.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UnsubscribeFromEmailCampaignsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The anonymous route of the unsubscribe link.</summary>
    private const string Route = "/api/public/unsubscribe";

    /// <summary>A valid token withdraws the client's consent and records the unsubscribe on the recipient, without a session.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Unsubscribe_ValidToken_WithdrawsConsentAndMarksTheRecipient()
    {
        using var scope = factory.Services.CreateScope();
        var seeded = await CampaignTestSeeds.SeedSendingRecipientAsync(scope.ServiceProvider);

        var response = await factory.CreateClient().PostAsJsonAsync(Route, new { token = seeded.Token });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var check = factory.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Clients.AsNoTracking().SingleAsync(c => c.Id == seeded.ClientId)).MarketingOptIn.Should().BeFalse();
        var recipient = await context.EmailCampaignRecipients.AsNoTracking().SingleAsync(r => r.CampaignId == seeded.CampaignId);
        recipient.Status.Should().Be(CampaignRecipientStatus.Unsubscribed);
    }

    /// <summary>Following the link again answers the same success and changes nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Unsubscribe_SameLinkTwice_StaysSuccessful()
    {
        using var scope = factory.Services.CreateScope();
        var seeded = await CampaignTestSeeds.SeedSendingRecipientAsync(scope.ServiceProvider);
        var anonymous = factory.CreateClient();

        await anonymous.PostAsJsonAsync(Route, new { token = seeded.Token });
        var again = await anonymous.PostAsJsonAsync(Route, new { token = seeded.Token });

        again.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>A recipient who was already mailed counts as unsubscribed too, so the campaign's stats show it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Unsubscribe_AfterTheMailWasSent_MarksTheSentRecipient()
    {
        using var scope = factory.Services.CreateScope();
        var seeded = await CampaignTestSeeds.SeedSendingRecipientAsync(scope.ServiceProvider);
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var recipient = await context.EmailCampaignRecipients.SingleAsync(r => r.CampaignId == seeded.CampaignId);
        recipient.MarkSent(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        var response = await factory.CreateClient().PostAsJsonAsync(Route, new { token = seeded.Token });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var check = factory.Services.CreateScope();
        var stored = await check.ServiceProvider.GetRequiredService<IHostpanelDbContext>()
            .EmailCampaignRecipients.AsNoTracking().SingleAsync(r => r.CampaignId == seeded.CampaignId);
        stored.Status.Should().Be(CampaignRecipientStatus.Unsubscribed);
    }

    /// <summary>Reads the primary key of the one recipient of a campaign.</summary>
    /// <param name="campaignId">The campaign.</param>
    /// <returns>The recipient's id.</returns>
    private async Task<int> RecipientIdAsync(int campaignId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>()
            .EmailCampaignRecipients.AsNoTracking().Where(r => r.CampaignId == campaignId).Select(r => r.Id).SingleAsync();
    }

    /// <summary>
    /// The sender mails the recipient and saves it as sent between the unsubscribe's read and its save. The save conflicts
    /// instead of answering 409: the recipient is read again, now sent, and marked unsubscribed.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Unsubscribe_SenderSavesTheRecipientFirst_ReadsItAgainAndSucceeds()
    {
        using var scope = factory.Services.CreateScope();
        var seeded = await CampaignTestSeeds.SeedSendingRecipientAsync(scope.ServiceProvider);
        var race = new RecipientRaceInterceptor(await RecipientIdAsync(seeded.CampaignId), races: 1);
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(race))));

        var response = await host.CreateClient().PostAsJsonAsync(Route, new { token = seeded.Token });

        race.Lost.Should().Be(1);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var check = factory.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Clients.AsNoTracking().SingleAsync(c => c.Id == seeded.ClientId)).MarketingOptIn.Should().BeFalse();
        (await context.EmailCampaignRecipients.AsNoTracking().SingleAsync(r => r.CampaignId == seeded.CampaignId)).Status
            .Should().Be(CampaignRecipientStatus.Unsubscribed);
    }

    /// <summary>
    /// The sender wins every attempt to mark the recipient. The reader still gets 204, because the consent is already
    /// withdrawn, which is what stops the mail; only the campaign's own count keeps the sender's outcome.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Unsubscribe_SenderWinsEveryAttempt_StillSucceedsWithTheConsentWithdrawn()
    {
        using var scope = factory.Services.CreateScope();
        var seeded = await CampaignTestSeeds.SeedSendingRecipientAsync(scope.ServiceProvider);
        var race = new RecipientRaceInterceptor(await RecipientIdAsync(seeded.CampaignId), races: int.MaxValue);
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(race))));

        var response = await host.CreateClient().PostAsJsonAsync(Route, new { token = seeded.Token });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        race.Lost.Should().BeGreaterThan(1);
        using var check = factory.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Clients.AsNoTracking().SingleAsync(c => c.Id == seeded.ClientId)).MarketingOptIn.Should().BeFalse();
        (await context.EmailCampaignRecipients.AsNoTracking().SingleAsync(r => r.CampaignId == seeded.CampaignId)).Status
            .Should().Be(CampaignRecipientStatus.Sent);
    }

    /// <summary>The token itself is not stored: asking with the stored hash instead of the token finds nobody.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Unsubscribe_WithTheStoredHashInsteadOfTheToken_IsRefusedNotFound()
    {
        using var scope = factory.Services.CreateScope();
        var seeded = await CampaignTestSeeds.SeedSendingRecipientAsync(scope.ServiceProvider);
        var stored = await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>()
            .EmailCampaignRecipients.AsNoTracking().Where(r => r.CampaignId == seeded.CampaignId).Select(r => r.UnsubscribeTokenHash).SingleAsync();

        var response = await factory.CreateClient().PostAsJsonAsync(Route, new { token = stored });

        stored.Should().NotBe(seeded.Token);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>A token that names nobody is refused with 404 and <c>CAMPAIGN_UNSUBSCRIBE_LINK_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Unsubscribe_UnknownToken_RefusesNotFound()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(Route, new { token = new string('a', 64) });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CAMPAIGN_UNSUBSCRIBE_LINK_NOT_FOUND);
    }

    /// <summary>A missing or oversized token is refused with 400 before any lookup.</summary>
    /// <param name="token">The token to send.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Unsubscribe_BlankToken_RefusesValidation(string token)
    {
        var response = await factory.CreateClient().PostAsJsonAsync(Route, new { token });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A token longer than any real one is refused with 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Unsubscribe_OversizedToken_RefusesValidation()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(Route, new { token = new string('a', 65) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
