using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Domain.Campaigns;
using Innovayse.Hostpanel.Domain.Campaigns.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Shared helpers for the email campaign Integration tests: the request bodies every test starts from, writing and
/// queueing a campaign through the admin API, and moving a campaign to the states only the sender reaches
/// (sending, with recipients) straight in the database.
/// </summary>
internal static class CampaignTestSeeds
{
    /// <summary>The route of the campaigns collection.</summary>
    public const string Route = "/api/admin/email-campaigns";

    /// <summary>One segment that picks every active client and takes no value.</summary>
    public static readonly object AllActive = new { filterType = "AllActive", filterValue = (string?)null };

    /// <summary>The body for writing a campaign.</summary>
    /// <param name="subject">The subject line.</param>
    /// <param name="body">The HTML body.</param>
    /// <param name="segments">The segments, or <see langword="null"/> for the single all-active segment.</param>
    /// <returns>The request body.</returns>
    public static object Body(string subject = "Autumn offer", string body = "<p>Hello</p>", IReadOnlyList<object>? segments = null) => new
    {
        subject,
        body,
        segments = segments ?? [AllActive],
    };

    /// <summary>Writes a campaign through the admin API and returns the id the server assigned.</summary>
    /// <param name="client">An authenticated admin client.</param>
    /// <param name="segments">The segments, or <see langword="null"/> for the single all-active segment.</param>
    /// <param name="subject">The subject line.</param>
    /// <returns>The new campaign's id.</returns>
    public static async Task<int> CreateAsync(HttpClient client, IReadOnlyList<object>? segments = null, string subject = "Autumn offer")
    {
        var response = await client.PostAsJsonAsync(Route, Body(subject, segments: segments));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<int>();
    }

    /// <summary>Queues a campaign through the admin API and requires it to be accepted.</summary>
    /// <param name="client">An authenticated admin client.</param>
    /// <param name="id">The campaign to queue.</param>
    /// <param name="at">When to send, or <see langword="null"/> to send now.</param>
    /// <returns>A task that completes once the campaign is queued.</returns>
    public static async Task ScheduleAsync(HttpClient client, int id, DateTimeOffset? at = null)
    {
        var response = await client.PostAsJsonAsync($"{Route}/{id}/schedule", new { at = at ?? DateTimeOffset.UtcNow.AddDays(1) });
        response.EnsureSuccessStatusCode();
    }

    /// <summary>The identity subject of the seeded admin, which is what a campaign it writes records as its author.</summary>
    /// <param name="factory">The factory whose host owns the identity store.</param>
    /// <returns>The admin's user id.</returns>
    public static async Task<string> AdminSubjectAsync(IntegrationTestFactory factory)
    {
        await factory.GetAdminTokenAsync();
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var admin = await userManager.FindByEmailAsync("admin-seed@innovayse.test");
        return admin!.Id;
    }

    /// <summary>
    /// Starts sending a scheduled campaign, as the sender does when it is due, and fixes one recipient in each delivery state:
    /// pending, sent, failed and unsubscribed.
    /// </summary>
    /// <param name="factory">The factory whose database to seed against.</param>
    /// <param name="id">The scheduled campaign.</param>
    /// <returns>A task that completes once the campaign is sending with its recipients.</returns>
    public static async Task SeedSendingWithRecipientsAsync(IntegrationTestFactory factory, int id)
    {
        var clients = new List<int>();
        for (var i = 0; i < 4; i++)
        {
            clients.Add((await ClientsTestSeeds.SeedClientAsync(factory)).ClientId);
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow;

        var campaign = await db.EmailCampaigns.Include(x => x.Recipients).SingleAsync(x => x.Id == id);
        campaign.StartSending();
        campaign.AddRecipient(clients[0], "pending@example.test", "en");
        campaign.AddRecipient(clients[1], "sent@example.test", "en").MarkSent(now);
        var failed = campaign.AddRecipient(clients[2], "failed@example.test", null);
        for (var attempt = 0; attempt < EmailCampaignRecipient.MaxAttempts; attempt++)
        {
            failed.RecordFailure("RELAY_REFUSED");
        }

        campaign.AddRecipient(clients[3], "unsubscribed@example.test", "hy").MarkUnsubscribed();
        await db.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Writes a client with a sign-in address who agreed to marketing email or did not, and optionally lives in a country.</summary>
    /// <param name="services">The service provider of the host the test drives.</param>
    /// <param name="optedIn">Whether the client agreed to marketing email.</param>
    /// <param name="country">The two-letter country code of the client's address, or <see langword="null"/> for none.</param>
    /// <returns>The client and its sign-in address.</returns>
    public static async Task<SeededIdentityClient> SeedAudienceClientAsync(IServiceProvider services, bool optedIn = true, string? country = null)
    {
        var context = services.GetRequiredService<IHostpanelDbContext>();
        var seeded = await EventHandlerTestHost.SeedIdentityClientAsync(services, context, "campaign");
        seeded.Client.UpdateSettings(true, false, null, null, false, false, optedIn, true, true);
        seeded.Client.UpdateAddress(null, null, null, null, null, country);
        await context.SaveChangesAsync(CancellationToken.None);
        return seeded;
    }

    /// <summary>
    /// Writes a campaign that is sending, with one recipient: a new client with a sign-in address, who agreed to marketing
    /// email or did not. The campaign goes through the domain methods, so it is in a state the sender can reach.
    /// </summary>
    /// <param name="services">The service provider of the host the test drives.</param>
    /// <param name="optedIn">Whether the client agreed to marketing email.</param>
    /// <param name="body">The HTML body of the campaign.</param>
    /// <returns>The campaign, the client, its address and the recipient's unsubscribe token.</returns>
    public static async Task<SeededCampaignRecipient> SeedSendingRecipientAsync(
        IServiceProvider services, bool optedIn = true, string body = "<p>Hello</p>")
    {
        var context = services.GetRequiredService<IHostpanelDbContext>();
        var seeded = await SeedAudienceClientAsync(services, optedIn);
        await SeedTemplateAsync(services);

        var now = services.GetRequiredService<IClock>().UtcNow;
        var campaign = EmailCampaign.Create("Autumn offer", body, "admin-1", [new(CampaignFilterType.AllActive, null)]);
        campaign.Schedule(null, now);
        campaign.StartSending();
        var recipient = campaign.AddRecipient(seeded.Client.Id, seeded.Email, "en");
        var token = recipient.IssueUnsubscribeToken();
        context.EmailCampaigns.Add(campaign);
        await context.SaveChangesAsync(CancellationToken.None);
        return new SeededCampaignRecipient(campaign.Id, seeded.Client.Id, seeded.Email, token);
    }

    /// <summary>
    /// Writes the campaign mail template, as the startup seed does on a real host (the test host does not run it, and seeding it for
    /// every test would add rows the migration round-trip test cannot take back to the old unique index).
    /// </summary>
    /// <param name="services">The service provider of the host the test drives.</param>
    /// <returns>A task that completes once every language version exists.</returns>
    public static Task SeedTemplateAsync(IServiceProvider services) =>
        EmailCampaignTemplateSeeder.EnsureSeededAsync(services.GetRequiredService<IHostpanelDbContext>(), CancellationToken.None);

    /// <summary>Fixes one more pending recipient on a sending campaign: a new client with a sign-in address who agreed to marketing email.</summary>
    /// <param name="services">The service provider of the host the test drives.</param>
    /// <param name="campaignId">The sending campaign.</param>
    /// <returns>The new recipient's address.</returns>
    public static async Task<string> AddRecipientAsync(IServiceProvider services, int campaignId)
    {
        var context = services.GetRequiredService<IHostpanelDbContext>();
        var seeded = await SeedAudienceClientAsync(services);
        var campaign = await context.EmailCampaigns.Include(x => x.Recipients).SingleAsync(x => x.Id == campaignId);
        campaign.AddRecipient(seeded.Client.Id, seeded.Email, "en");
        await context.SaveChangesAsync(CancellationToken.None);
        return seeded.Email;
    }

    /// <summary>
    /// Writes a client with a sign-in address who agreed to marketing email, then takes the account out of the active state, as
    /// staff do when they suspend a client.
    /// </summary>
    /// <param name="services">The service provider of the host the test drives.</param>
    /// <returns>The suspended client and its sign-in address.</returns>
    public static async Task<SeededIdentityClient> SeedSuspendedAudienceClientAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<IHostpanelDbContext>();
        var seeded = await SeedAudienceClientAsync(services);
        seeded.Client.Suspend(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);
        return seeded;
    }

    /// <summary>
    /// Gives a client the sign-in address of another, written in capitals, as two accounts that reach the same mailbox do. The
    /// identity store refuses a second account for one address, so the address is changed underneath it.
    /// </summary>
    /// <param name="services">The service provider of the host the test drives.</param>
    /// <param name="client">The client whose account takes the address.</param>
    /// <param name="address">The address to share.</param>
    /// <returns>A task that completes once the account holds the address.</returns>
    public static async Task ShareAddressAsync(IServiceProvider services, Client client, string address)
    {
        var identity = services.GetRequiredService<AppDbContext>();
        var user = await identity.Users.SingleAsync(u => u.Id == client.UserId);
        user.Email = address.ToUpperInvariant();
        await identity.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Sets when a client last signed in, as the identity store holds it, and optionally moves the client's creation back in time,
    /// so a client can be made to have been away for a while without waiting.
    /// </summary>
    /// <param name="services">The service provider of the host the test drives.</param>
    /// <param name="client">The client whose account is changed.</param>
    /// <param name="lastSignInDaysAgo">How many days ago the client last signed in, or <see langword="null"/> for never.</param>
    /// <param name="createdDaysAgo">How many days ago the client record was created, or <see langword="null"/> to leave it as it is.</param>
    /// <returns>A task that completes once both are saved.</returns>
    public static async Task SetSignInHistoryAsync(IServiceProvider services, Client client, int? lastSignInDaysAgo, int? createdDaysAgo = null)
    {
        var now = DateTimeOffset.UtcNow;
        var identity = services.GetRequiredService<AppDbContext>();
        var user = await identity.Users.SingleAsync(u => u.Id == client.UserId);
        user.LastLoginAt = lastSignInDaysAgo is null ? null : now.AddDays(-lastSignInDaysAgo.Value);
        await identity.SaveChangesAsync(CancellationToken.None);

        if (createdDaysAgo is not null)
        {
            var context = services.GetRequiredService<IHostpanelDbContext>();
            var tracked = await context.Clients.SingleAsync(c => c.Id == client.Id);
            tracked.Backdate(now.AddDays(-createdDaysAgo.Value));
            await context.SaveChangesAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Gives a client an active monthly service, of a new product unless one is named. The billing period starts the given
    /// number of days ago, so the service renews about a month after that moment.
    /// </summary>
    /// <param name="services">The service provider of the host the test drives.</param>
    /// <param name="clientId">The client who owns the service.</param>
    /// <param name="productId">The product to give, or <see langword="null"/> for a new one.</param>
    /// <param name="periodStartedDaysAgo">How many days ago the billing period started.</param>
    /// <returns>The product's primary key.</returns>
    public static async Task<int> SeedServiceAsync(IServiceProvider services, int clientId, int? productId = null, int periodStartedDaysAgo = 0)
    {
        var context = services.GetRequiredService<IHostpanelDbContext>();
        var now = DateTimeOffset.UtcNow;
        if (productId is null)
        {
            var group = ProductGroup.Create($"campaign-{Guid.NewGuid():N}", null);
            context.ProductGroups.Add(group);
            await context.SaveChangesAsync(CancellationToken.None);

            var product = Product.Create(group.Id, "Campaign product", null, null, null, null, ProductType.SharedHosting, 0m, 0m, now);
            context.Products.Add(product);
            await context.SaveChangesAsync(CancellationToken.None);
            productId = product.Id;
        }

        var service = ClientService.Create(clientId, productId.Value, BillingCycle.Monthly, now);
        service.Activate($"ref-{Guid.NewGuid():N}", now.AddDays(-periodStartedDaysAgo), now);
        context.ClientServices.Add(service);
        await context.SaveChangesAsync(CancellationToken.None);
        return productId.Value;
    }

    /// <summary>Finishes delivery of a campaign that is sending, so it is in the sent state.</summary>
    /// <param name="factory">The factory whose database to seed against.</param>
    /// <param name="id">The sending campaign.</param>
    /// <returns>A task that completes once the campaign is sent.</returns>
    public static async Task SeedSentAsync(IntegrationTestFactory factory, int id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var campaign = await db.EmailCampaigns.SingleAsync(x => x.Id == id);
        campaign.MarkSent(scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
        await db.SaveChangesAsync(CancellationToken.None);
    }
}
