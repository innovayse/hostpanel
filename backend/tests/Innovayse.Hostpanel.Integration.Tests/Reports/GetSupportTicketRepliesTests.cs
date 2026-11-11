using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/support-ticket-replies</c> against Postgres: reply counts per admin per
/// day for the month.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetSupportTicketRepliesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A staff reply posted today is counted in the Support Ticket Replies report.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CountsAStaffReplyToday()
    {
        var marker = $"Reply{Guid.NewGuid():N}";
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, marker);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // A real department: a ticket's department is a foreign key.
            var departmentId = await SupportTestSeeds.SeedDepartmentAsync(db);
            var ticket = Ticket.Create(clientId, "Help", "I need help", departmentId, TicketPriority.Medium, DateTimeOffset.UtcNow);
            ticket.AddReply("On it", marker, isStaffReply: true, DateTimeOffset.UtcNow);
            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/support-ticket-replies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("rows").EnumerateArray().Should().Contain(
            r => r.GetProperty("adminName").GetString() == marker && r.GetProperty("total").GetInt32() >= 1);
    }

    /// <summary>An out-of-range month is refused before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnInvalidMonth()
    {
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/reports/support-ticket-replies?year=2024&month=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/support-ticket-replies");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "support-ticket-replies");

        var response = await client.GetAsync("/api/reports/support-ticket-replies");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
