using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// <c>ClientService.AutoRenew</c> has a database default of true, which EF would apply in place of an
/// explicit false unless the property's sentinel is moved off the CLR default. These tests pin that a
/// stored false stays false and that a new service is renewed by default.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AutoRenewPersistenceTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A service switched to manual renewal is stored and reloaded as false, not replaced by the default.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ExplicitFalse_IsStoredAsFalseAsync()
    {
        var id = await InsertAsync(service => service.ConfigureAutomation(false, false, null, false, null));

        (await ReloadAsync(id)).Should().BeFalse("the database default must not overwrite an explicit false");
    }

    /// <summary>A service created without touching the flag is renewed automatically.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Default_ReloadsAsTrueAsync()
    {
        var id = await InsertAsync(_ => { });

        (await ReloadAsync(id)).Should().BeTrue();
    }

    /// <summary>Inserts a service after applying <paramref name="configure"/>.</summary>
    /// <param name="configure">Changes made to the new service before it is saved.</param>
    /// <returns>The new service's id.</returns>
    private async Task<int> InsertAsync(Action<ClientService> configure)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var productId = await DomainsTestSeeds.SeedHostingProductAsync(context);
        var service = ClientService.Create(clientId, productId, billingCycle: BillingCycle.Monthly, now: DateTimeOffset.UtcNow);
        configure(service);
        context.Set<ClientService>().Add(service);
        await context.SaveChangesAsync();
        return service.Id;
    }

    /// <summary>Reads <c>AutoRenew</c> back through a fresh, untracked query.</summary>
    /// <param name="id">The service id.</param>
    /// <returns>The stored flag.</returns>
    private async Task<bool> ReloadAsync(int id)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.Set<ClientService>().AsNoTracking().Where(s => s.Id == id).Select(s => s.AutoRenew).SingleAsync();
    }
}
