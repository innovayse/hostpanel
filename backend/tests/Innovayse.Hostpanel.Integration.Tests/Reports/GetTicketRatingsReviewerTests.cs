using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/ticket-ratings-reviewer</c> against Postgres: one row per rated reply,
/// newest first.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetTicketRatingsReviewerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A rated ticket appears in the Ticket Ratings Reviewer report.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ShowsARatedTicket()
    {
        var marker = $"Review{Guid.NewGuid():N}";
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, marker);
        int ticketId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // A real department: a ticket's department is a foreign key.
            var departmentId = await SupportTestSeeds.SeedDepartmentAsync(db);
            var ticket = Ticket.Create(clientId, $"Subject-{marker}", "Body", departmentId, TicketPriority.Low, TestMoments.Seeded);
            ticket.LeaveFeedback(9, "All good", marker, scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();
            ticketId = ticket.Id;
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/ticket-ratings-reviewer?minRating=9");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("rows").EnumerateArray().Should().Contain(r => r.GetProperty("ticketId").GetInt32() == ticketId);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/ticket-ratings-reviewer");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "ticket-ratings-reviewer");

        var response = await client.GetAsync("/api/reports/ticket-ratings-reviewer");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
