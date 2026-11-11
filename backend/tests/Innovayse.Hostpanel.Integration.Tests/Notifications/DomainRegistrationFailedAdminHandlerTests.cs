using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Domains.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Tests for <c>DomainRegistrationFailedAdminHandler</c>: a failed domain registration alerts the platform admin.
/// Each test builds its own host from <see cref="IntegrationTestFactory"/> with the one port this platform
/// does not own (the SMTP relay) replaced by <see cref="FakeEmailSender"/>, publishes the event exactly once
/// through the real message bus and asserts the handler's one effect happened exactly once.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DomainRegistrationFailedAdminHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The fixed moment the seeded rows and the events are stamped with.</summary>
    private static readonly DateTimeOffset SeededAt = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A failed domain registration alerts the configured platform admin exactly once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DomainRegistrationFailed_NotifiesAdminExactlyOnceAsync()
    {
        var emailSender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, emailSender, NotificationsTestSeeds.AdminEmail);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var (client, _) = await EventHandlerTestHost.SeedIdentityClientAsync(scope.ServiceProvider, context, "notification-event");
        await NotificationsTestSeeds.EnsureTemplateAsync(context, "domain-registration-failed-admin", "Domain registration failed");
        await NotificationsTestSeeds.EnsureTemplateAsync(context, "domain-registration-failed", "Your domain registration failed");

        await scope.ServiceProvider.PublishAndWaitAsync(
            new DomainRegistrationFailedEvent(client.Id, "example.test", 1, "registrar rejected") { OccurredAt = SeededAt });

        var toAdmin = Assert.Single(emailSender.Sent, m => m.To == NotificationsTestSeeds.AdminEmail);
        Assert.NotNull(toAdmin);
    }

    /// <summary>
    /// With neither staff address configured the admin alert is skipped without throwing, so the chain the
    /// client handler shares is not retried: the owning client is mailed exactly once and nothing else is sent.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DomainRegistrationFailed_WithNoStaffAddress_MailsOnlyTheClientOnceAsync()
    {
        var emailSender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, emailSender);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var (client, email) = await EventHandlerTestHost.SeedIdentityClientAsync(scope.ServiceProvider, context, "notification-event");
        await NotificationsTestSeeds.EnsureTemplateAsync(context, "domain-registration-failed-admin", "Domain registration failed");
        await NotificationsTestSeeds.EnsureTemplateAsync(context, "domain-registration-failed", "Your domain registration failed");

        await scope.ServiceProvider.PublishAndWaitAsync(
            new DomainRegistrationFailedEvent(client.Id, "example.test", 1, "registrar rejected") { OccurredAt = SeededAt });

        var only = Assert.Single(emailSender.Sent);
        Assert.Equal(email, only.To);
    }

    /// <summary>With only the contact inbox configured the admin alert falls back to it, once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DomainRegistrationFailed_WithOnlyContactAddress_AlertsTheContactInboxOnceAsync()
    {
        const string contactEmail = "contact-fallback@example.test";
        var emailSender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, emailSender, contactEmail: contactEmail);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var (client, _) = await EventHandlerTestHost.SeedIdentityClientAsync(scope.ServiceProvider, context, "notification-event");
        await NotificationsTestSeeds.EnsureTemplateAsync(context, "domain-registration-failed-admin", "Domain registration failed");
        await NotificationsTestSeeds.EnsureTemplateAsync(context, "domain-registration-failed", "Your domain registration failed");

        await scope.ServiceProvider.PublishAndWaitAsync(
            new DomainRegistrationFailedEvent(client.Id, "example.test", 1, "registrar rejected") { OccurredAt = SeededAt });

        Assert.Single(emailSender.Sent, m => m.To == contactEmail);
    }
}
