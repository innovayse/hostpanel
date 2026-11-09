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
/// <c>GET /api/reports/ticket-feedback-comments</c> against Postgres: one row per piece of
/// feedback, newest first.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetTicketFeedbackCommentsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A rated ticket's feedback appears in the Ticket Feedback Comments report with the staff placeholder name.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ShowsARatedTicket()
    {
        var marker = $"Feedback{Guid.NewGuid():N}";
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, marker);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // A real department: a ticket's department is a foreign key.
            var departmentId = await SupportTestSeeds.SeedDepartmentAsync(db);
            var ticket = Ticket.Create(clientId, $"Subject-{marker}", "Body", departmentId, TicketPriority.Low, DateTimeOffset.UtcNow);
            ticket.Assign(77);
            ticket.LeaveFeedback(8, "Great support", marker, scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/ticket-feedback-comments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("rows").EnumerateArray().Should().Contain(
            r => r.GetProperty("feedbackLeftBy").GetString() == marker
                && r.GetProperty("staffName").GetString() == "Staff #77"
                && r.GetProperty("rating").GetInt32() == 8);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/ticket-feedback-comments");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "ticket-feedback-comments");

        var response = await client.GetAsync("/api/reports/ticket-feedback-comments");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
