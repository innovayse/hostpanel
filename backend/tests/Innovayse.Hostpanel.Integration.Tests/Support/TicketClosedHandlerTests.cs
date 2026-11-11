using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Innovayse.Hostpanel.Domain.Support.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// Tests for <c>TicketClosedHandler</c>: a closed ticket notifies the owning client.
/// Each test builds its own host from <see cref="IntegrationTestFactory"/> with the one port this platform
/// does not own (the SMTP relay) replaced by <see cref="FakeEmailSender"/>, publishes the event exactly once
/// through the real message bus and asserts the handler's one effect happened exactly once.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TicketClosedHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The fixed moment the seeded rows and the events are stamped with.</summary>
    private static readonly DateTimeOffset SeededAt = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Closing a ticket notifies the owning client.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TicketClosed_SendsTicketClosedEmailToClientAsync()
    {
        var emailSender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, emailSender);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var (client, email) = await EventHandlerTestHost.SeedIdentityClientAsync(scope.ServiceProvider, context, "ticket-notify", "Jane", "Doe");
        var departmentId = await SupportTestSeeds.SeedDepartmentAsync(context);
        var ticket = Ticket.Create(client.Id, "Help", "Something's broken", departmentId, TicketPriority.Medium, SeededAt);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync(CancellationToken.None);

        await scope.ServiceProvider.PublishAndWaitAsync(new TicketClosedEvent(ticket.Id, client.Id) { OccurredAt = SeededAt });

        var message = Assert.Single(emailSender.Sent);
        Assert.Equal(email, message.To);
        Assert.Contains("Closed", message.Subject, StringComparison.Ordinal);
    }

    /// <summary>
    /// A ticket subject carrying a link and a script reaches the HTML body as text: every character
    /// that would open markup is encoded, so a client cannot plant a link or a script in the mail.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TicketClosed_EncodesMarkupInTheTicketSubjectInTheBodyAsync()
    {
        var emailSender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, emailSender);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        const string subject = "<a href=\"https://phish.example\">Verify</a><script>steal()</script>";
        var (client, _) = await EventHandlerTestHost.SeedIdentityClientAsync(scope.ServiceProvider, context, "ticket-notify", "Jane", "Doe");
        var departmentId = await SupportTestSeeds.SeedDepartmentAsync(context);
        var ticket = Ticket.Create(client.Id, subject, "Something's broken", departmentId, TicketPriority.Medium, SeededAt);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync(CancellationToken.None);

        await scope.ServiceProvider.PublishAndWaitAsync(new TicketClosedEvent(ticket.Id, client.Id) { OccurredAt = SeededAt });

        var message = Assert.Single(emailSender.Sent);
        Assert.DoesNotContain("<a href", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", message.Body, StringComparison.Ordinal);
        Assert.Contains("&lt;a href=&quot;https://phish.example&quot;&gt;Verify&lt;/a&gt;", message.Body, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;steal()&lt;/script&gt;", message.Body, StringComparison.Ordinal);
    }

    /// <summary>Closing a ticket for a client that does not exist sends no email.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TicketClosed_WhenClientNotFound_DoesNotSendAsync()
    {
        var emailSender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, emailSender);
        using var scope = host.Services.CreateScope();

        await scope.ServiceProvider.PublishAndWaitAsync(new TicketClosedEvent(1, 999_999_999) { OccurredAt = SeededAt });

        Assert.Empty(emailSender.Sent);
    }

    /// <summary>
    /// A late copy of the event naming a ticket that is gone or belongs to another client (the demo reset
    /// reused the ids) does not mail the client the event names.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TicketClosed_ForATicketThatIsNotTheClients_DoesNotSendAsync()
    {
        var emailSender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, emailSender);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var (client, _) = await EventHandlerTestHost.SeedIdentityClientAsync(scope.ServiceProvider, context, "ticket-notify", "Jane", "Doe");
        var (stranger, _) = await EventHandlerTestHost.SeedIdentityClientAsync(scope.ServiceProvider, context, "ticket-notify-stranger");
        var departmentId = await SupportTestSeeds.SeedDepartmentAsync(context);
        var ticket = Ticket.Create(stranger.Id, "Help", "Something's broken", departmentId, TicketPriority.Medium, SeededAt);
        using (scope.ServiceProvider.GetRequiredService<IDomainEventGate>().Suppress())
        {
            context.Tickets.Add(ticket);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        await scope.ServiceProvider.PublishAndWaitAsync(new TicketClosedEvent(ticket.Id, client.Id) { OccurredAt = SeededAt });
        await scope.ServiceProvider.PublishAndWaitAsync(new TicketClosedEvent(int.MaxValue, client.Id) { OccurredAt = SeededAt });

        Assert.Empty(emailSender.Sent);
    }
}
