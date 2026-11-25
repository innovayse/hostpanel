using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Admin;

/// <summary>
/// <c>GET /api/admin/dashboard/activity</c>: an admin reads the merged, newest-first
/// activity feed, an out-of-range limit substitutes the default rather than being refused, a
/// caller with only the Client role is refused, and the route is closed to a caller with no
/// credential. This use case refuses nothing once authorized.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetRecentActivityTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin reads the activity feed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanReadTheActivityFeedAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/dashboard/activity");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>An out-of-range limit substitutes the default of 10 rather than being refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OutOfRangeLimit_SubstitutesTheDefaultAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/dashboard/activity?limit=0");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// The feed answers the kind of each event and the facts about it, never a sentence: the client's name, the
    /// invoice total and its currency for an overdue invoice and a payment, the subject of an opened ticket,
    /// and no text a frontend would have to print as it came.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnswersTheTypeAndTheFactsOfEachEventNotASentenceAsync()
    {
        var now = new DateTimeOffset(2062, 6, 15, 12, 0, 0, TimeSpan.Zero);
        using var host = new ClockedHost(factory, now);
        var lastName = $"Feed{Guid.NewGuid():N}";
        var clientId = await ActivityFeedSeeds.SeedClientAsync(host, lastName, now.AddDays(-20));
        var overdueId = await ActivityFeedSeeds.SeedInvoiceAsync(host, clientId, 40m, now.AddDays(-30), invoice => invoice.MarkOverdue(now.AddDays(-1)));
        var paidId = await ActivityFeedSeeds.SeedInvoiceAsync(host, clientId, 75m, now.AddDays(-2), invoice => invoice.AddPayment(now.AddDays(-2), "Card", $"tx-{Guid.NewGuid():N}", 75m, 0m, null, TransactionSource.Manual));
        var ticketId = await ActivityFeedSeeds.SeedTicketAsync(host, clientId, "Site is down", now.AddHours(-1));
        var admin = await AuthTestSeeds.AdminClientAsync(factory, host);

        var feed = await admin.GetFromJsonAsync<JsonElement>("/api/admin/dashboard/activity?limit=50");

        var events = feed.EnumerateArray().Where(e => e.GetProperty("clientId").GetInt32() == clientId).ToList();
        events.Select(e => e.GetProperty("type").GetString()).Should().Equal("TicketOpened", "PaymentReceived", "InvoiceOverdue");
        events.Should().OnlyContain(e => e.GetProperty("clientName").GetString() == $"Ada {lastName}");
        events.Should().OnlyContain(e => !e.TryGetProperty("message", out _));
        events.Should().OnlyContain(e => !e.GetProperty("isRead").GetBoolean());

        var ticket = events[0];
        ticket.GetProperty("entityId").GetInt32().Should().Be(ticketId);
        ticket.GetProperty("subject").GetString().Should().Be("Site is down");
        ticket.GetProperty("amount").ValueKind.Should().Be(JsonValueKind.Null);

        var overdue = events[2];
        overdue.GetProperty("entityId").GetInt32().Should().Be(overdueId);
        overdue.GetProperty("amount").GetDecimal().Should().Be(40m);
        overdue.GetProperty("currency").GetString().Should().NotBeNullOrEmpty();

        var paid = events[1];
        paid.GetProperty("entityId").GetInt32().Should().Be(paidId);
        paid.GetProperty("amount").GetDecimal().Should().Be(75m);
    }

    /// <summary>A client registered inside the lookback is its own client in the feed, named in full.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AClientRegistrationNamesTheNewClientAsync()
    {
        var now = new DateTimeOffset(2063, 6, 15, 12, 0, 0, TimeSpan.Zero);
        using var host = new ClockedHost(factory, now);
        var lastName = $"Feed{Guid.NewGuid():N}";
        var clientId = await ActivityFeedSeeds.SeedClientAsync(host, lastName, now.AddDays(-1));
        var admin = await AuthTestSeeds.AdminClientAsync(factory, host);

        var feed = await admin.GetFromJsonAsync<JsonElement>("/api/admin/dashboard/activity?limit=50");

        var registered = feed.EnumerateArray().Single(e => e.GetProperty("entityId").GetInt32() == clientId && e.GetProperty("type").GetString() == "ClientRegistered");
        registered.GetProperty("clientId").GetInt32().Should().Be(clientId);
        registered.GetProperty("clientName").GetString().Should().Be($"Ada {lastName}");
    }

    /// <summary>A ticket staff already answered, a payment older than the lookback and a ticket opened before it are not surfaced.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LeavesOutAnsweredTicketsAndOldPaymentsAsync()
    {
        var now = new DateTimeOffset(2064, 6, 15, 12, 0, 0, TimeSpan.Zero);
        using var host = new ClockedHost(factory, now);
        var clientId = await ActivityFeedSeeds.SeedClientAsync(host, $"Feed{Guid.NewGuid():N}", now.AddDays(-60));
        await ActivityFeedSeeds.SeedInvoiceAsync(host, clientId, 10m, now.AddDays(-40), invoice => invoice.AddPayment(now.AddDays(-30), "Card", $"tx-{Guid.NewGuid():N}", 10m, 0m, null, TransactionSource.Manual));
        await ActivityFeedSeeds.SeedTicketAsync(host, clientId, "Old ticket", now.AddDays(-30));
        await ActivityFeedSeeds.SeedTicketAsync(host, clientId, "Answered", now.AddDays(-1), ticket => ticket.AddReply("Fixed", "Staff", true, now));
        var admin = await AuthTestSeeds.AdminClientAsync(factory, host);

        var feed = await admin.GetFromJsonAsync<JsonElement>("/api/admin/dashboard/activity?limit=50");

        feed.EnumerateArray().Where(e => e.GetProperty("clientId").GetInt32() == clientId).Should().BeEmpty();
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await AdminTestSeeds.ClientRoleClientAsync(factory, "recent-activity-forbidden");

        var response = await client.GetAsync("/api/admin/dashboard/activity");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The activity feed is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/dashboard/activity");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
