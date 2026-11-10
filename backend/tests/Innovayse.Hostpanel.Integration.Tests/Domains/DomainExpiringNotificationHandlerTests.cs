using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Events;
using Innovayse.Hostpanel.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Tests for <c>DomainExpiringNotificationHandler</c>: a domain nearing expiry notifies the owning client.
/// Each test builds its own host from <see cref="IntegrationTestFactory"/> with the one port this platform
/// does not own (the SMTP relay) replaced by <see cref="FakeEmailSender"/>, publishes the event exactly once
/// through the real message bus and asserts the handler's one effect. The event reaches the handler through a
/// durable queue and can arrive after the demo reset reused its ids, so a domain that is not the client's is
/// covered too.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DomainExpiringNotificationHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The slug of the template the handler renders.</summary>
    private const string TemplateSlug = "domain-expiring";

    /// <summary>A domain approaching its expiry date notifies the owning client exactly once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DomainExpiring_NotifiesOwningClientExactlyOnceAsync()
    {
        var emailSender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, emailSender);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var (client, email) = await EventHandlerTestHost.SeedIdentityClientAsync(scope.ServiceProvider, context, "domain-event");
        await EnsureTemplateAsync(context);
        var domainId = await SeedDomainAsync(scope.ServiceProvider, context, client.Id);

        await scope.ServiceProvider.PublishAndWaitAsync(new DomainExpiringEvent(domainId, client.Id, "example.test", DateTimeOffset.UtcNow.AddDays(7)) { OccurredAt = DateTimeOffset.UtcNow });

        var message = Assert.Single(emailSender.Sent);
        Assert.Equal(email, message.To);
    }

    /// <summary>
    /// An event naming a domain that belongs to another client (an id the demo reset reused) or no domain at
    /// all mails nobody.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DomainExpiring_ForADomainThatIsNotTheClients_SendsNothingAsync()
    {
        var emailSender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, emailSender);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var (client, _) = await EventHandlerTestHost.SeedIdentityClientAsync(scope.ServiceProvider, context, "domain-event");
        var (stranger, _) = await EventHandlerTestHost.SeedIdentityClientAsync(scope.ServiceProvider, context, "domain-event-stranger");
        await EnsureTemplateAsync(context);
        var strangerDomainId = await SeedDomainAsync(scope.ServiceProvider, context, stranger.Id);

        await scope.ServiceProvider.PublishAndWaitAsync(new DomainExpiringEvent(strangerDomainId, client.Id, "example.test", DateTimeOffset.UtcNow.AddDays(7)) { OccurredAt = DateTimeOffset.UtcNow });
        await scope.ServiceProvider.PublishAndWaitAsync(new DomainExpiringEvent(int.MaxValue, client.Id, "example.test", DateTimeOffset.UtcNow.AddDays(7)) { OccurredAt = DateTimeOffset.UtcNow });

        Assert.Empty(emailSender.Sent);
    }

    /// <summary>Adds the handler's template once; the class shares one database and the slug is unique.</summary>
    /// <param name="context">The scope's data context.</param>
    /// <returns>A task that completes once the template exists.</returns>
    private static async Task EnsureTemplateAsync(IHostpanelDbContext context)
    {
        if (!await context.EmailTemplates.AnyAsync(t => t.Slug == TemplateSlug, CancellationToken.None))
        {
            context.EmailTemplates.Add(EmailTemplate.Create(TemplateSlug, "en", "Domain expiring soon: {{ domain.name }}", "Heads up", null));
            await context.SaveChangesAsync(CancellationToken.None);
        }
    }

    /// <summary>Seeds a domain owned by the client, with the seeding's own events discarded.</summary>
    /// <param name="services">The scope's services.</param>
    /// <param name="context">The scope's data context.</param>
    /// <param name="clientId">The owning client.</param>
    /// <returns>The saved domain's id.</returns>
    private static async Task<int> SeedDomainAsync(IServiceProvider services, IHostpanelDbContext context, int clientId)
    {
        using (services.GetRequiredService<IDomainEventGate>().Suppress())
        {
            var domain = RegisteredDomain.Register(
                clientId, $"event-{Guid.NewGuid():N}.test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1),
                autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
            context.Domains.Add(domain);
            await context.SaveChangesAsync(CancellationToken.None);
            return domain.Id;
        }
    }
}
