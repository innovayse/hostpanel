using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Innovayse.Hostpanel.Domain.Support.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// Tests for <c>TicketRepliedHandler</c>: a reply notifies the other side of the conversation.
/// Each test builds its own host from <see cref="IntegrationTestFactory"/> with the one port this platform
/// does not own (the SMTP relay) replaced by <see cref="FakeEmailSender"/>, publishes the event exactly once
/// through the real message bus and asserts the handler's one effect happened exactly once.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TicketRepliedHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A staff reply notifies the client who owns the ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TicketReplied_WhenStaffReplies_NotifiesClientAsync()
    {
        var emailSender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, emailSender);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var (client, email) = await EventHandlerTestHost.SeedIdentityClientAsync(scope.ServiceProvider, context, "ticket-notify", "Jane", "Doe");
        var departmentId = await SupportTestSeeds.SeedDepartmentAsync(context);
        var ticket = Ticket.Create(client.Id, "Help", "Something's broken", departmentId, TicketPriority.Medium, DateTimeOffset.UtcNow);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync(CancellationToken.None);

        await scope.ServiceProvider.PublishAndWaitAsync(new TicketRepliedEvent(ticket.Id, IsStaffReply: true) { OccurredAt = DateTimeOffset.UtcNow });

        var message = Assert.Single(emailSender.Sent);
        Assert.Equal(email, message.To);
        Assert.Contains("New Reply", message.Subject, StringComparison.Ordinal);
    }

    /// <summary>
    /// The reply email carries the ticket's subject as the client typed it: the subject line is
    /// plain text, so markup in it arrives as the same characters, neither dropped nor encoded.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TicketReplied_RendersTheTicketSubjectInTheEmailSubjectAsync()
    {
        var emailSender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, emailSender);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        const string subject = "Printer <b>offline</b> & jammed";
        var (client, _) = await EventHandlerTestHost.SeedIdentityClientAsync(scope.ServiceProvider, context, "ticket-notify", "Jane", "Doe");
        var departmentId = await SupportTestSeeds.SeedDepartmentAsync(context);
        var ticket = Ticket.Create(client.Id, subject, "Something's broken", departmentId, TicketPriority.Medium, DateTimeOffset.UtcNow);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync(CancellationToken.None);

        await scope.ServiceProvider.PublishAndWaitAsync(new TicketRepliedEvent(ticket.Id, IsStaffReply: true) { OccurredAt = DateTimeOffset.UtcNow });

        var message = Assert.Single(emailSender.Sent);
        Assert.Contains($"#{ticket.Id}: {subject}", message.Subject, StringComparison.Ordinal);
    }

    /// <summary>A client reply notifies the ticket's assigned department.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TicketReplied_WhenClientReplies_NotifiesDepartmentAsync()
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
        var ticket = Ticket.Create(clientId, "Help", "Something's broken", department.Id, TicketPriority.Medium, DateTimeOffset.UtcNow);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync(CancellationToken.None);

        await scope.ServiceProvider.PublishAndWaitAsync(new TicketRepliedEvent(ticket.Id, IsStaffReply: false) { OccurredAt = DateTimeOffset.UtcNow });

        var message = Assert.Single(emailSender.Sent);
        Assert.Equal(departmentEmail, message.To);
        Assert.Contains("New Reply", message.Subject, StringComparison.Ordinal);
    }
}
