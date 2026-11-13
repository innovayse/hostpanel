using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Seeds and hosts for the renewal read models and commands the Services and Domains modules give
/// recurring billing: active services and domains written straight to the database, with domain
/// events suppressed so seeding provisions nothing.
/// </summary>
internal static class RenewalTestSeeds
{
    /// <summary>Builds a host whose domain events are not dispatched.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <returns>The isolated host.</returns>
    public static WebApplicationFactory<Program> SuppressedHost(IntegrationTestFactory factory) =>
        factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.AddScoped<IDomainEventGate, SuppressedDomainEventGate>();
        }));

    /// <summary>Seeds a client, a product and one active monthly service at 5.00 a month.</summary>
    /// <param name="host">The host to seed through.</param>
    /// <param name="nextRenewalAt">The service's renewal date, or <see langword="null"/> for an undated one.</param>
    /// <returns>The client, product and service ids.</returns>
    public static async Task<SeededRenewingService> SeedActiveServiceAsync(
        WebApplicationFactory<Program> host, DateTimeOffset? nextRenewalAt)
    {
        using var scope = host.Services.CreateScope();
        var ids = await ServicesTestSeeds.SeedClientAndProductAsync(scope, 5m);
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var service = ClientService.Create(ids.ClientId, ids.ProductId, BillingCycle.Monthly, DateTimeOffset.UtcNow);
        await ServicesTestSeeds.AddActivatedAsync(context, service, "panel-ref", DateTimeOffset.UtcNow);
        service.ChangeBillingTerms(new ServiceBillingTerms(BillingCycle.Monthly, 1, 5m, 5m, nextRenewalAt, null, null, null));
        await context.SaveChangesAsync(CancellationToken.None);

        return new SeededRenewingService(ids.ClientId, ids.ProductId, service.Id);
    }

    /// <summary>Seeds an active, registrar-linked domain for a fresh client.</summary>
    /// <param name="host">The host to seed through.</param>
    /// <param name="expiresAt">The domain's expiry.</param>
    /// <param name="autoRenew">Whether the domain renews automatically.</param>
    /// <returns>The saved domain.</returns>
    public static async Task<RegisteredDomain> SeedActiveDomainAsync(
        WebApplicationFactory<Program> host, DateTimeOffset expiresAt, bool autoRenew)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);

        var domain = RegisteredDomain.Register(clientId, $"{Guid.NewGuid():N}.test", TestMoments.Seeded, expiresAt, autoRenew, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        await DomainsTestSeeds.AddActivatedAsync(context, domain, "REF-STUB", domain.ExpiresAt, DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);
        return domain;
    }
}
