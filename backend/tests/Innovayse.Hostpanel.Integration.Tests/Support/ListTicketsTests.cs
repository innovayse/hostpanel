using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>GET /api/tickets</c>: staff can list every support ticket, and a plain client is refused:
/// the route carries the class-level <c>Admin</c> policy.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListTicketsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The fixed moment the seeded rows and the events are stamped with.</summary>
    private static readonly DateTimeOffset SeededAt = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Staff can list every support ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanListTicketsAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/tickets");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A registered client, who is not staff, is forbidden.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ClientIsForbiddenAsync()
    {
        var (client, _) = await SupportTestSeeds.RegisterClientAsync(factory, "list-tickets-client");

        var response = await client.GetAsync("/api/tickets");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A caller with no credential cannot list tickets.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/tickets");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>The staff list names the client who opened each ticket, so it does not have to fall back to a bare client number.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRowsNameTheClientWhoOpenedTheTicketAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        int ticketId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var client = Client.Create($"user-{Guid.NewGuid():N}", "Grace", "Hopper", $"grace-{Guid.NewGuid():N}@example.test", SeededAt);
            context.Clients.Add(client);
            await context.SaveChangesAsync(CancellationToken.None);
            var departmentId = await SupportTestSeeds.SeedDepartmentAsync(context);
            var ticket = Ticket.Create(client.Id, "Named requestor", "Hello", departmentId, TicketPriority.Medium, SeededAt);
            context.Tickets.Add(ticket);
            await context.SaveChangesAsync(CancellationToken.None);
            ticketId = ticket.Id;
        }

        var response = await admin.GetAsync("/api/tickets?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = body.RootElement.GetProperty("items").EnumerateArray().Single(t => t.GetProperty("id").GetInt32() == ticketId);
        row.GetProperty("clientName").GetString().Should().Be("Grace Hopper");
    }

    /// <summary>A status filter that names no ticket status is refused with 400, not silently ignored.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownStatusFilterIsRejectedAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/tickets?status=Archived");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The status filter binds by name and keeps only tickets in that status.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StatusFilterKeepsOnlyThatStatusAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var openId = await SupportTestSeeds.CreateAdminTicketAsync(factory, admin);
        var closedId = await SupportTestSeeds.CreateAdminTicketAsync(factory, admin);
        (await admin.PostAsync($"/api/tickets/{closedId}/close", content: null)).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        var response = await admin.GetAsync($"/api/tickets?status={nameof(TicketStatus.Closed)}&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = body.RootElement.GetProperty("items").EnumerateArray().ToList();
        items.Should().OnlyContain(t => t.GetProperty("status").GetString() == nameof(TicketStatus.Closed));
        items.Select(t => t.GetProperty("id").GetInt32()).Should().Contain(closedId).And.NotContain(openId);
    }

    /// <summary>The flagged filter keeps only tickets staff flagged.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FlaggedFilterKeepsOnlyFlaggedTicketsAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var unflaggedId = await SupportTestSeeds.CreateAdminTicketAsync(factory, admin);
        var flaggedId = await SupportTestSeeds.CreateAdminTicketAsync(factory, admin);
        (await admin.PostAsync($"/api/tickets/{flaggedId}/toggle-flag", content: null)).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        var response = await admin.GetAsync("/api/tickets?flagged=true&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = body.RootElement.GetProperty("items").EnumerateArray().ToList();
        items.Should().OnlyContain(t => t.GetProperty("isFlagged").GetBoolean());
        items.Select(t => t.GetProperty("id").GetInt32()).Should().Contain(flaggedId).And.NotContain(unflaggedId);
    }
}
