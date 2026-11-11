using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Tests for <c>InvoiceCreatedHandler</c>: a newly created invoice notifies the owning client.
/// Each test builds its own host from <see cref="IntegrationTestFactory"/> with the one port this platform
/// does not own (the SMTP relay) replaced by <see cref="FakeEmailSender"/>, publishes the event exactly once
/// through the real message bus and asserts the handler's one effect happened exactly once.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class InvoiceCreatedHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The fixed moment the published event says the change happened.</summary>
    private static readonly DateTimeOffset EventMoment = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A newly created invoice notifies the owning client exactly once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InvoiceCreated_NotifiesOwningClientExactlyOnceAsync()
    {
        var emailSender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, emailSender, NotificationsTestSeeds.AdminEmail);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var (client, email) = await EventHandlerTestHost.SeedIdentityClientAsync(scope.ServiceProvider, context, "notification-event");

        await scope.ServiceProvider.PublishAndWaitAsync(new InvoiceCreatedEvent(1, client.Id, 19.90m, "USD") { OccurredAt = EventMoment });

        var message = Assert.Single(emailSender.Sent);
        Assert.Equal(email, message.To);
        Assert.Contains("19.90", message.Body);
        Assert.Contains("USD", message.Body);
    }
}
