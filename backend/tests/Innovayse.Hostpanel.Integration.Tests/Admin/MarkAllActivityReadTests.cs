using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin;

/// <summary>
/// <c>POST /api/admin/dashboard/activity/read-all</c>: an admin marks every event of the feed read, a second call
/// answers the same, a caller with only the Client role is refused, and the route is closed to a caller with no
/// credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class MarkAllActivityReadTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route under test.</summary>
    private const string Route = "/api/admin/dashboard/activity/read-all";

    /// <summary>The route that marks one event read.</summary>
    private const string MarkOneRoute = "/api/admin/dashboard/activity/read";

    /// <summary>The feed route the read state is checked through.</summary>
    private const string FeedRoute = "/api/admin/dashboard/activity?limit=50";

    /// <summary>Every event the feed holds is read once the admin marks them all, and a second call changes nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MarksEveryEventOfTheFeedReadAsync()
    {
        var now = new DateTimeOffset(2066, 6, 15, 12, 0, 0, TimeSpan.Zero);
        using var host = new ClockedHost(factory, now);
        var clientId = await ActivityFeedSeeds.SeedClientAsync(host, $"ReadAll{Guid.NewGuid():N}", now.AddDays(-2));
        await ActivityFeedSeeds.SeedTicketAsync(host, clientId, "Help", now.AddHours(-1));
        var admin = await AuthTestSeeds.AdminClientAsync(factory, host);
        var before = await EventsOfAsync(admin, clientId);
        before.Should().HaveCount(2).And.OnlyContain(e => !e.GetProperty("isRead").GetBoolean());

        var first = await admin.PostAsync(Route, content: null);
        var second = await admin.PostAsync(Route, content: null);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var after = await EventsOfAsync(admin, clientId);
        after.Should().HaveCount(2).And.OnlyContain(e => e.GetProperty("isRead").GetBoolean());
    }

    /// <summary>Events the admin already read stay as they are and mark-all stores the rest, one row per event.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WhenSomeEventsAreAlreadyRead_StoresTheRestAsync()
    {
        var now = new DateTimeOffset(2068, 6, 15, 12, 0, 0, TimeSpan.Zero);
        using var host = new ClockedHost(factory, now);
        var clientId = await ActivityFeedSeeds.SeedClientAsync(host, $"Partly{Guid.NewGuid():N}", now.AddDays(-2));
        await ActivityFeedSeeds.SeedTicketAsync(host, clientId, "Help", now.AddHours(-1));
        await ActivityFeedSeeds.SeedTicketAsync(host, clientId, "Help again", now.AddHours(-2));
        var admin = await AuthTestSeeds.AdminClientAsync(factory, host);
        var keys = (await EventsOfAsync(admin, clientId)).Select(e => e.GetProperty("key").GetString()!).ToList();
        (await admin.PostAsJsonAsync(MarkOneRoute, new { eventKey = keys[0] })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await admin.PostAsync(Route, content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var after = await EventsOfAsync(admin, clientId);
        after.Should().HaveCount(3).And.OnlyContain(e => e.GetProperty("isRead").GetBoolean());
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.ActivityReads.CountAsync(r => keys.Contains(r.EventKey), CancellationToken.None)).Should().Be(3);
    }

    /// <summary>Marking one event after mark-all in the same flow answers 204 and adds no second row.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MarkingOneEventAfterMarkAll_SucceedsAndStoresNoDuplicateAsync()
    {
        var now = new DateTimeOffset(2069, 6, 15, 12, 0, 0, TimeSpan.Zero);
        using var host = new ClockedHost(factory, now);
        var clientId = await ActivityFeedSeeds.SeedClientAsync(host, $"After{Guid.NewGuid():N}", now.AddDays(-2));
        await ActivityFeedSeeds.SeedTicketAsync(host, clientId, "Help", now.AddHours(-1));
        var admin = await AuthTestSeeds.AdminClientAsync(factory, host);
        var keys = (await EventsOfAsync(admin, clientId)).Select(e => e.GetProperty("key").GetString()!).ToList();

        var all = await admin.PostAsync(Route, content: null);
        var one = await admin.PostAsJsonAsync(MarkOneRoute, new { eventKey = keys[0] });

        all.StatusCode.Should().Be(HttpStatusCode.NoContent);
        one.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.ActivityReads.CountAsync(r => keys.Contains(r.EventKey), CancellationToken.None)).Should().Be(2);
    }

    /// <summary>A single mark racing a mark-all, over and over, never fails either call and leaves every event read.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ASingleMarkRacingMarkAll_LeavesEveryEventReadAsync()
    {
        var now = new DateTimeOffset(2067, 6, 15, 12, 0, 0, TimeSpan.Zero);
        using var host = new ClockedHost(factory, now);
        var clientId = await ActivityFeedSeeds.SeedClientAsync(host, $"Race{Guid.NewGuid():N}", now.AddDays(-2));
        await ActivityFeedSeeds.SeedTicketAsync(host, clientId, "Help", now.AddHours(-1));
        await ActivityFeedSeeds.SeedTicketAsync(host, clientId, "Help again", now.AddHours(-2));
        var admin = await AuthTestSeeds.AdminClientAsync(factory, host);
        var keys = (await EventsOfAsync(admin, clientId)).Select(e => e.GetProperty("key").GetString()).ToList();

        var calls = new List<Task<HttpResponseMessage>> { admin.PostAsync(Route, content: null) };
        calls.AddRange(keys.Select(key => admin.PostAsJsonAsync(MarkOneRoute, new { eventKey = key })));
        var responses = await Task.WhenAll(calls);

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.NoContent);
        var after = await EventsOfAsync(admin, clientId);
        after.Should().HaveCount(3).And.OnlyContain(e => e.GetProperty("isRead").GetBoolean());
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await AdminTestSeeds.ClientRoleClientAsync(factory, "mark-all-activity-read-forbidden");

        var response = await client.PostAsync(Route, content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsync(Route, content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Reads the feed and returns the events about a client.</summary>
    /// <param name="admin">The signed-in admin.</param>
    /// <param name="clientId">The client the events are about.</param>
    /// <returns>The events as JSON.</returns>
    private static async Task<List<JsonElement>> EventsOfAsync(HttpClient admin, int clientId)
    {
        var feed = await admin.GetFromJsonAsync<JsonElement>(FeedRoute);
        return feed.EnumerateArray().Where(e => e.GetProperty("clientId").GetInt32() == clientId).ToList();
    }
}
