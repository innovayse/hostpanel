using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// Pins the optimistic concurrency token on the aggregates two staff actions or a cron and a staff
/// action can load at the same time: of two writers that read the same row only one save wins.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RowVersionTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Two units of work load the same ticket and each change it: the first save wins, the second
    /// conflicts on the ticket's row version and the first change is not lost.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Ticket_TwoConcurrentWriters_SecondSaveConflictsAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var ticketId = await SeedTicketAsync(clientId);

        using var first = factory.Services.CreateScope();
        using var second = factory.Services.CreateScope();
        var firstContext = first.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var secondContext = second.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        // Both contexts load the ticket before either saves.
        var firstTicket = await firstContext.Tickets.SingleAsync(t => t.Id == ticketId);
        var secondTicket = await secondContext.Tickets.SingleAsync(t => t.Id == ticketId);

        firstTicket.ToggleFlag();
        secondTicket.ChangePriority(TicketPriority.High);

        await firstContext.SaveChangesAsync(CancellationToken.None);
        var act = () => secondContext.SaveChangesAsync(CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyConflictException>();

        using var check = factory.Services.CreateScope();
        var saved = await check.ServiceProvider.GetRequiredService<IHostpanelDbContext>()
            .Tickets.AsNoTracking().SingleAsync(t => t.Id == ticketId);
        saved.IsFlagged.Should().BeTrue();
        saved.Priority.Should().Be(TicketPriority.Medium);
    }

    /// <summary>Opens a medium-priority ticket for the client in a new department and returns its id.</summary>
    /// <param name="clientId">The client the ticket belongs to.</param>
    /// <returns>The new ticket's id.</returns>
    private async Task<int> SeedTicketAsync(int clientId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var department = Department.Create($"Support {Guid.NewGuid():N}", "support@example.test");
        context.Departments.Add(department);
        await context.SaveChangesAsync(CancellationToken.None);

        var ticket = Ticket.Create(
            clientId, "Concurrent edit", "Two staff open this ticket at once.", department.Id, TicketPriority.Medium, TestMoments.Seeded);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync(CancellationToken.None);
        return ticket.Id;
    }
}
