using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Innovayse.Hostpanel.Domain.Support.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// Tests for <c>TicketCreatedHandler</c>: a created ticket notifies its department.
/// Each test builds its own host from <see cref="IntegrationTestFactory"/> with the one port this platform
/// does not own (the SMTP relay) replaced by <see cref="FakeEmailSender"/>, publishes the event exactly once
/// through the real message bus and asserts the handler's one effect happened exactly once.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TicketCreatedHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The fixed moment the seeded rows and the events are stamped with.</summary>
    private static readonly DateTimeOffset SeededAt = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A created ticket with a department that has an email notifies that department.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TicketCreated_WhenDepartmentHasEmail_SendsTicketCreatedEmailAsync()
    {
        var emailSender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, emailSender);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var departmentEmail = $"support-{Guid.NewGuid():N}@example.com";
        var department = Department.Create($"Support-{Guid.NewGuid():N}", departmentEmail);
        context.Departments.Add(department);
        await context.SaveChangesAsync(CancellationToken.None);

        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var ticket = Ticket.Create(clientId, "Help", "Something's broken", department.Id, TicketPriority.Medium, SeededAt);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync(CancellationToken.None);

        await scope.ServiceProvider.PublishAndWaitAsync(new TicketCreatedEvent(ticket.Id, clientId, department.Id) { OccurredAt = SeededAt });

        var message = Assert.Single(emailSender.Sent);
        Assert.Equal(departmentEmail, message.To);
        Assert.Contains("Created", message.Subject, StringComparison.Ordinal);
    }

    /// <summary>A created ticket whose department does not exist sends no email.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TicketCreated_WhenDepartmentNotFound_DoesNotSendAsync()
    {
        var emailSender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, emailSender);
        using var scope = host.Services.CreateScope();

        await scope.ServiceProvider.PublishAndWaitAsync(new TicketCreatedEvent(1, 1, 999_999_999) { OccurredAt = SeededAt });

        Assert.Empty(emailSender.Sent);
    }

    /// <summary>
    /// A late copy of the event naming a ticket that now belongs to another client (the demo reset reused the
    /// id) does not mail the department about it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TicketCreated_ForATicketOfAnotherClient_DoesNotSendAsync()
    {
        var emailSender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, emailSender);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var department = Department.Create($"Support-{Guid.NewGuid():N}", $"support-{Guid.NewGuid():N}@example.com");
        context.Departments.Add(department);
        await context.SaveChangesAsync(CancellationToken.None);
        var ownerId = await DomainsTestSeeds.SeedClientAsync(context);
        var otherClientId = await DomainsTestSeeds.SeedClientAsync(context);
        var ticket = Ticket.Create(ownerId, "Help", "Something's broken", department.Id, TicketPriority.Medium, SeededAt);
        using (scope.ServiceProvider.GetRequiredService<IDomainEventGate>().Suppress())
        {
            context.Tickets.Add(ticket);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        await scope.ServiceProvider.PublishAndWaitAsync(new TicketCreatedEvent(ticket.Id, otherClientId, department.Id) { OccurredAt = SeededAt });

        Assert.Empty(emailSender.Sent);
    }
}
