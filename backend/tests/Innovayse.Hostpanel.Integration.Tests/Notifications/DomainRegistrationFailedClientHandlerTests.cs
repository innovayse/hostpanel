using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Domains.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Tests for <c>DomainRegistrationFailedClientHandler</c>: a failed domain registration notifies the owning client.
/// Each test builds its own host from <see cref="IntegrationTestFactory"/> with the one port this platform
/// does not own (the SMTP relay) replaced by <see cref="FakeEmailSender"/>, publishes the event exactly once
/// through the real message bus and asserts the handler's one effect happened exactly once.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DomainRegistrationFailedClientHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// The same failed domain registration also notifies the owning client exactly once, through a second,
    /// independent handler for the one event.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DomainRegistrationFailed_NotifiesOwningClientExactlyOnceAsync()
    {
        var emailSender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, emailSender, NotificationsTestSeeds.AdminEmail);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var (client, email) = await EventHandlerTestHost.SeedIdentityClientAsync(scope.ServiceProvider, context, "notification-event");
        await NotificationsTestSeeds.EnsureTemplateAsync(context, "domain-registration-failed-admin", "Domain registration failed");
        await NotificationsTestSeeds.EnsureTemplateAsync(context, "domain-registration-failed", "Your domain registration failed");

        await scope.ServiceProvider.PublishAndWaitAsync(
            new DomainRegistrationFailedEvent(client.Id, "example.test", 1, "registrar rejected") { OccurredAt = TestMoments.Seeded });

        var toClient = Assert.Single(emailSender.Sent, m => m.To == email);
        Assert.NotNull(toClient);
    }
}
