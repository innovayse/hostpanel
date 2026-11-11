using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Domain.Domains.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Tests for <c>DomainRenewalFailedAdminHandler</c>: a domain renewal that needs a person mails the
/// configured staff address, seeding the <c>domain-renewal-failed-admin</c> template on first use.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DomainRenewalFailedAdminHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The fixed moment the seeded rows and the events are stamped with.</summary>
    private static readonly DateTimeOffset SeededAt = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The staff address configured on the test host.</summary>
    private const string AdminEmail = "ops@example.test";

    /// <summary>Publishing the event sends one staff mail under the renewal-failed subject, and the template row exists.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DomainRenewalFailed_MailsStaffOnceAndSeedsTheTemplate()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender, adminEmail: AdminEmail);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var name = $"{Guid.NewGuid():N}.test";

        await bus.PublishAsync(new DomainRenewalFailedEvent(42, 7, name, 99, "Registrar refused the paid renewal.") { OccurredAt = SeededAt });

        for (var i = 0; i < 50 && !sender.Sent.Any(m => m.To == AdminEmail); i++)
        {
            await Task.Delay(200);
        }

        var mail = sender.Sent.Should().ContainSingle(m => m.To == AdminEmail).Which;
        mail.Subject.Should().Contain("Domain renewal needs attention").And.Contain(name);
        mail.Body.Should().Contain("Registrar refused the paid renewal.").And.Contain("invoice #99");
        (await context.EmailTemplates.AsNoTracking().AnyAsync(t => t.Slug == RenewalTemplatesSeeder.DomainFailedAdminSlug))
            .Should().BeTrue();
        (await context.EmailTemplates.AsNoTracking().AnyAsync(t => t.Slug == RenewalTemplatesSeeder.ReminderSlug))
            .Should().BeTrue();
    }

    /// <summary>
    /// An alert about a domain no invoice was raised for (invoice id 0, an unpriceable TLD) does not
    /// name an "invoice #0".
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DomainRenewalFailed_WithoutInvoice_NamesNoInvoice()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender, adminEmail: AdminEmail);
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var name = $"{Guid.NewGuid():N}.test";

        await bus.PublishAsync(new DomainRenewalFailedEvent(42, 7, name, 0, "No renew price; no renewal invoice was raised.") { OccurredAt = SeededAt });

        for (var i = 0; i < 50 && !sender.Sent.Any(m => m.To == AdminEmail && m.Subject.Contains(name, StringComparison.Ordinal)); i++)
        {
            await Task.Delay(200);
        }

        var mail = sender.Sent.Should().ContainSingle(m => m.To == AdminEmail && m.Subject.Contains(name, StringComparison.Ordinal)).Which;
        mail.Body.Should().Contain("No renew price").And.NotContain("invoice #");
    }

    /// <summary>
    /// With no admin address configured (the case on every tier today) the alert falls back to the
    /// contact inbox rather than reaching nobody.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DomainRenewalFailed_FallsBackToTheContactInbox()
    {
        const string contact = "contact@example.test";
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender, contactEmail: contact);
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var name = $"{Guid.NewGuid():N}.test";

        await bus.PublishAsync(new DomainRenewalFailedEvent(42, 7, name, 99, "Registrar refused the paid renewal.") { OccurredAt = SeededAt });

        for (var i = 0; i < 50 && !sender.Sent.Any(m => m.To == contact); i++)
        {
            await Task.Delay(200);
        }

        sender.Sent.Should().ContainSingle(m => m.To == contact)
            .Which.Subject.Should().Contain(name);
    }

    /// <summary>With neither address configured nothing is sent (the handler logs an error naming the setting instead).</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DomainRenewalFailed_WithNoAddress_SendsNothing()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender);
        using var scope = host.Services.CreateScope();

        await scope.ServiceProvider.PublishAndWaitAsync(new DomainRenewalFailedEvent(42, 7, $"{Guid.NewGuid():N}.test", 99, "x") { OccurredAt = SeededAt });

        sender.Sent.Should().BeEmpty();
    }
}
