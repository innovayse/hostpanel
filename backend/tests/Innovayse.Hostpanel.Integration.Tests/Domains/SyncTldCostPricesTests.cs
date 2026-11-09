using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.SyncTldCostPrices;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the <c>SyncTldCostPrices</c> use case, the scheduled job dispatched through
/// the real bus (<c>IMessageBus.InvokeAsync</c>). The admin-triggered one-pass sync is
/// <c>SyncTldCostPricesNow</c>, tested in <see cref="SyncTldCostPricesNowTests"/>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SyncTldCostPricesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Syncing cost prices groups every TLD configuration by its registrar module and updates
    /// the cost columns of the ones the (fake) provider's pricing list names: the query
    /// <c>SyncTldCostPricesHandler</c> loads and groups by <see cref="TldConfig.RegistrarModule"/>.
    /// Uses <see cref="FakeRegistrarProviderFactory"/> so the sync never resolves a real,
    /// credentialed registrar client.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SyncTldCostPrices_UpdatesCostPricesForAMatchingTldAsync()
    {
        var pricingProvider = new FakeTldPricingProvider
        {
            PricingToReturn =
            [
                new TldPricing(
                    "synced",
                    "USD",
                    Register: new Dictionary<int, decimal> { [1] = 9m },
                    Transfer: new Dictionary<int, decimal> { [1] = 10m },
                    Renew: new Dictionary<int, decimal> { [1] = 11m }),
            ],
        };
        var registrars = new FakeRegistrarProviderFactory();
        registrars.Register(RegistrarModule.NameAm, pricingProvider);

        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IRegistrarProviderFactory>(registrars)));

        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var tld = TldConfig.Create(
            "synced", RegistrarModule.NameAm, "USD", "USD", isEnabled: true, sortOrder: 0, categories: []);
        context.TldConfigs.Add(tld);
        await context.SaveChangesAsync(CancellationToken.None);

        await bus.InvokeAsync(new SyncTldCostPricesCommand(), CancellationToken.None);

        Assert.Equal(1, pricingProvider.PricingCallCount);
        var reloaded = await context.TldConfigs.AsNoTracking().FirstAsync(t => t.Id == tld.Id, CancellationToken.None);
        Assert.Equal(9m, reloaded.CostRegister[1]);
    }
}
