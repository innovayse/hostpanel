using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin;

/// <summary>
/// <c>POST /api/admin/dashboard/activity/read</c>: an admin marks one event of the feed read, the feed then says so for
/// that admin, marking twice answers the same and stores one row, a missing or oversized key is refused with a
/// validation error, a caller with only the Client role is refused, and the route is closed to a caller with no
/// credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class MarkActivityReadTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route under test.</summary>
    private const string Route = "/api/admin/dashboard/activity/read";

    /// <summary>The feed route the read state is checked through.</summary>
    private const string FeedRoute = "/api/admin/dashboard/activity?limit=50";

    /// <summary>The event the feed returns for the client is unread, then read once it is marked.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MarksTheEventReadForTheCallerAsync()
    {
        var now = new DateTimeOffset(2065, 6, 15, 12, 0, 0, TimeSpan.Zero);
        using var host = new ClockedHost(factory, now);
        var clientId = await ActivityFeedSeeds.SeedClientAsync(host, $"Read{Guid.NewGuid():N}", now.AddDays(-1));
        var admin = await AuthTestSeeds.AdminClientAsync(factory, host);
        var before = await EventOfAsync(admin, clientId);
        before.GetProperty("isRead").GetBoolean().Should().BeFalse();

        var response = await admin.PostAsJsonAsync(Route, new { eventKey = before.GetProperty("key").GetString() });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var after = await EventOfAsync(admin, clientId);
        after.GetProperty("isRead").GetBoolean().Should().BeTrue();
    }

    /// <summary>Marking an event twice answers 204 both times and stores one row.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MarkingTwiceStoresOneRowAsync()
    {
        var key = $"ClientRegistered:{Guid.NewGuid():N}:1";
        var admin = await AdminTestSeeds.AdminClientAsync(factory);

        var first = await admin.PostAsJsonAsync(Route, new { eventKey = key });
        var second = await admin.PostAsJsonAsync(Route, new { eventKey = key });

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.ActivityReads.CountAsync(r => r.EventKey == key, CancellationToken.None)).Should().Be(1);
    }

    /// <summary>Many calls marking the same new event at once all answer 204 and store one row, never a 500.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ConcurrentMarksOfTheSameEvent_AllSucceedAndStoreOneRowAsync()
    {
        var key = $"ClientRegistered:{Guid.NewGuid():N}:2";
        var admin = await AdminTestSeeds.AdminClientAsync(factory);

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => admin.PostAsJsonAsync(Route, new { eventKey = key })));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.ActivityReads.CountAsync(r => r.EventKey == key, CancellationToken.None)).Should().Be(1);
    }

    /// <summary>
    /// Marking the same event twice and then another one through the store in a single scope fails no save and stores
    /// each event once: nothing stays tracked from the repeated mark.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RepeatedMarkInOneScope_DoesNotBreakALaterMarkAsync()
    {
        var userId = $"user-{Guid.NewGuid():N}";
        var first = $"ClientRegistered:{Guid.NewGuid():N}:3";
        var second = $"ClientRegistered:{Guid.NewGuid():N}:4";
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IActivityReadStore>();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var readAt = DateTimeOffset.UtcNow;

        await store.MarkReadAsync(userId, [first], readAt, CancellationToken.None);
        await store.MarkReadAsync(userId, [first, second], readAt, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        (await context.ActivityReads.CountAsync(r => r.UserId == userId, CancellationToken.None)).Should().Be(2);
    }

    /// <summary>A key that is missing or longer than the column holds is a validation error.</summary>
    /// <param name="key">The key sent.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RefusesAnEmptyKeyAsync(string key)
    {
        var admin = await AdminTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(Route, new { eventKey = key });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A key longer than the column holds is a validation error.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAKeyLongerThanTheColumnAsync()
    {
        var admin = await AdminTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(Route, new { eventKey = new string('k', ActivityRead.MaxEventKeyLength + 1) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await AdminTestSeeds.ClientRoleClientAsync(factory, "mark-activity-read-forbidden");

        var response = await client.PostAsJsonAsync(Route, new { eventKey = "k" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(Route, new { eventKey = "k" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Reads the feed and returns the one event about a client.</summary>
    /// <param name="admin">The signed-in admin.</param>
    /// <param name="clientId">The client the event is about.</param>
    /// <returns>The event as JSON.</returns>
    private static async Task<JsonElement> EventOfAsync(HttpClient admin, int clientId)
    {
        var feed = await admin.GetFromJsonAsync<JsonElement>(FeedRoute);
        return feed.EnumerateArray().Single(e => e.GetProperty("clientId").GetInt32() == clientId);
    }
}
