using Innovayse.Hostpanel.Domain.Clients.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Tests for <c>ClientRegisteredHandler</c>: a newly registered client is sent the welcome email.
/// Each test builds its own host from <see cref="IntegrationTestFactory"/> with the one port this platform
/// does not own (the SMTP relay) replaced by <see cref="FakeEmailSender"/>, publishes the event exactly once
/// through the real message bus and asserts the handler's one effect happened exactly once.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ClientRegisteredHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A newly registered client is sent the welcome email exactly once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ClientRegistered_SendsWelcomeEmailExactlyOnceAsync()
    {
        var emailSender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, emailSender, NotificationsTestSeeds.AdminEmail);
        using var scope = host.Services.CreateScope();

        var email = $"welcome-{Guid.NewGuid():N}@example.test";

        await scope.ServiceProvider.PublishAndWaitAsync(new ClientCreatedEvent(1, $"subject-{Guid.NewGuid():N}", email, TestMoments.Seeded));

        var message = Assert.Single(emailSender.Sent);
        Assert.Equal(email, message.To);
    }
}
