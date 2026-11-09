using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Events;
using Innovayse.Hostpanel.Domain.Notifications;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Tests for <c>DomainExpiredHandler</c>: an expired domain suspends its linked service and notifies the client.
/// Each test builds its own host from <see cref="IntegrationTestFactory"/> with the one port this platform
/// does not own (the SMTP relay) replaced by <see cref="FakeEmailSender"/>, publishes the event through the
/// real message bus and asserts the handler's effects. The event reaches the handler through a durable queue,
/// so the replay cases (a second delivery, a domain renewed since, a domain the demo reset removed) are
/// covered too.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DomainExpiredHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The slug of the template the handler renders.</summary>
    private const string TemplateSlug = "domain-expired";

    /// <summary>
    /// An expired domain with a linked, active hosting service suspends that service exactly once
    /// and notifies the owning client exactly once.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DomainExpired_SuspendsLinkedServiceAndNotifiesExactlyOnceAsync()
    {
        var emailSender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, emailSender);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var (clientId, email, serviceId, domainId) = await SeedExpiredDomainAsync(scope.ServiceProvider, context, expire: true);

        await scope.ServiceProvider.PublishAndWaitAsync(new DomainExpiredEvent(domainId, clientId, serviceId) { OccurredAt = DateTimeOffset.UtcNow });

        var message = Assert.Single(emailSender.Sent);
        Assert.Equal(email, message.To);

        // AsNoTracking, not FindAsync: FindAsync would return the locally-tracked instance this
        // test's own context already holds (added above, never mutated by it), masking whatever
        // the nested SuspendServiceCommand actually wrote through its own scope.
        var reloaded = await context.ClientServices.AsNoTracking()
            .FirstAsync(s => s.Id == serviceId, CancellationToken.None);
        Assert.Equal(ServiceStatus.Suspended, reloaded.Status);
        Assert.Equal(SuspensionSource.DomainExpired, reloaded.SuspensionSource);
    }

    /// <summary>
    /// A second delivery of the same event leaves the service suspended once with its source intact:
    /// the services module refuses to suspend a suspended service, and the handler does not throw.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DomainExpired_Redelivered_KeepsTheSingleSuspensionAsync()
    {
        var emailSender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, emailSender);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var (clientId, _, serviceId, domainId) = await SeedExpiredDomainAsync(scope.ServiceProvider, context, expire: true);
        var evt = new DomainExpiredEvent(domainId, clientId, serviceId) { OccurredAt = DateTimeOffset.UtcNow };

        await scope.ServiceProvider.PublishAndWaitAsync(evt);
        await scope.ServiceProvider.PublishAndWaitAsync(evt);

        var reloaded = await context.ClientServices.AsNoTracking()
            .FirstAsync(s => s.Id == serviceId, CancellationToken.None);
        Assert.Equal(ServiceStatus.Suspended, reloaded.Status);
        Assert.Equal(SuspensionSource.DomainExpired, reloaded.SuspensionSource);
    }

    /// <summary>
    /// A late copy of the event for a domain that is active again (renewed after it expired) does not
    /// suspend the linked service.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DomainExpired_ForADomainNoLongerExpired_LeavesTheServiceActiveAsync()
    {
        var emailSender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, emailSender);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var (clientId, _, serviceId, domainId) = await SeedExpiredDomainAsync(scope.ServiceProvider, context, expire: false);

        await scope.ServiceProvider.PublishAndWaitAsync(new DomainExpiredEvent(domainId, clientId, serviceId) { OccurredAt = DateTimeOffset.UtcNow });

        var reloaded = await context.ClientServices.AsNoTracking()
            .FirstAsync(s => s.Id == serviceId, CancellationToken.None);
        Assert.Equal(ServiceStatus.Active, reloaded.Status);
    }

    /// <summary>
    /// An event whose domain no longer exists, or now belongs to another client (the demo reset reused the
    /// id), neither suspends the service it names nor mails anyone.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DomainExpired_ForADomainOfAnotherClient_DoesNothingAsync()
    {
        var emailSender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, emailSender);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var (clientId, _, serviceId, _) = await SeedExpiredDomainAsync(scope.ServiceProvider, context, expire: true);
        int strangerDomainId;
        using (scope.ServiceProvider.GetRequiredService<IDomainEventGate>().Suppress())
        {
            strangerDomainId = await DomainsTestSeeds.SeedActiveDomainAsync(context);
        }

        await scope.ServiceProvider.PublishAndWaitAsync(new DomainExpiredEvent(strangerDomainId, clientId, serviceId) { OccurredAt = DateTimeOffset.UtcNow });
        await scope.ServiceProvider.PublishAndWaitAsync(new DomainExpiredEvent(int.MaxValue, clientId, serviceId) { OccurredAt = DateTimeOffset.UtcNow });

        Assert.Empty(emailSender.Sent);
        var reloaded = await context.ClientServices.AsNoTracking()
            .FirstAsync(s => s.Id == serviceId, CancellationToken.None);
        Assert.Equal(ServiceStatus.Active, reloaded.Status);
    }

    /// <summary>
    /// Seeds a client with a sign-in address, an active hosting service and a domain linked to it, with the
    /// seeding's own events discarded so the only effects observed are the handler's.
    /// </summary>
    /// <param name="services">The scope's services.</param>
    /// <param name="context">The scope's data context.</param>
    /// <param name="expire">Whether the domain is moved to Expired; otherwise it stays Active, as after a renewal.</param>
    /// <returns>The client id, its sign-in address, the service id and the domain id.</returns>
    private static async Task<(int ClientId, string Email, int ServiceId, int DomainId)> SeedExpiredDomainAsync(
        IServiceProvider services, IHostpanelDbContext context, bool expire)
    {
        var (client, email) = await EventHandlerTestHost.SeedIdentityClientAsync(services, context, "domain-event");
        var productId = await DomainsTestSeeds.SeedHostingProductAsync(context);
        var service = ClientService.Create(client.Id, productId, BillingCycle.Monthly, DateTimeOffset.UtcNow);
        var domain = RegisteredDomain.Register(
            client.Id, $"expired-{Guid.NewGuid():N}.test", DateTimeOffset.UtcNow.AddYears(-1), DateTimeOffset.UtcNow,
            autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        using (services.GetRequiredService<IDomainEventGate>().Suppress())
        {
            await ServicesTestSeeds.AddActivatedAsync(context, service, "provisioning-ref", DateTimeOffset.UtcNow);
            await context.SaveChangesAsync(CancellationToken.None);

            await DomainsTestSeeds.AddActivatedAsync(context, domain, "registrar-ref", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            domain.LinkService(service.Id);
            if (expire)
            {
                domain.MarkExpired(DateTimeOffset.UtcNow);
            }

            await context.SaveChangesAsync(CancellationToken.None);
        }

        // The slug is unique and the class shares one database, so a sibling test may have seeded it.
        if (!await context.EmailTemplates.AnyAsync(t => t.Slug == TemplateSlug, CancellationToken.None))
        {
            context.EmailTemplates.Add(EmailTemplate.Create(TemplateSlug, "en", "Domain expired: {{ domain.name }}", "Sorry", null));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        return (client.Id, email, service.Id, domain.Id);
    }
}
