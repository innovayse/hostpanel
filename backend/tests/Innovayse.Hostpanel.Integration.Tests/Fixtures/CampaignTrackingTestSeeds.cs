using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>Helpers for the open and click tracking tests: a sending campaign whose recipient already holds a tracking token.</summary>
internal static class CampaignTrackingTestSeeds
{
    /// <summary>
    /// Writes a sending campaign with one recipient (see <see cref="CampaignTestSeeds.SeedSendingRecipientAsync"/>) and issues the
    /// recipient's tracking token, so the open pixel and the tracked links can be followed.
    /// </summary>
    /// <param name="services">The service provider of the host the test drives.</param>
    /// <param name="optedIn">Whether the client agreed to marketing email.</param>
    /// <param name="body">The HTML body of the campaign.</param>
    /// <returns>The campaign, the client, its address and both tokens.</returns>
    public static async Task<SeededTrackedRecipient> SeedAsync(IServiceProvider services, bool optedIn = true, string body = "<p>Hello</p>")
    {
        var seeded = await CampaignTestSeeds.SeedSendingRecipientAsync(services, optedIn, body);
        var context = services.GetRequiredService<IHostpanelDbContext>();
        var recipient = await context.EmailCampaignRecipients.SingleAsync(r => r.CampaignId == seeded.CampaignId);
        var trackingToken = recipient.IssueTrackingToken();
        await context.SaveChangesAsync(CancellationToken.None);
        return new SeededTrackedRecipient(seeded.CampaignId, seeded.ClientId, seeded.Email, seeded.Token, trackingToken);
    }
}
