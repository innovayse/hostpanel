using System.Net.Http.Headers;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Domains;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A host where one invented zone is configured for a recording registrar whose plugin manifest declares the domain fields a test
/// gives. Nothing about the zone is real: the host takes it, like every field, from the manifest, so a test can say what a
/// registry asks for without a plugin and without a real extension.
/// </summary>
public sealed class DomainFieldsScenario
{
    /// <summary>The sell price of a year under a zone started with prices, in USD.</summary>
    private const decimal PricedZoneSellPrice = 10m;

    /// <summary>Initializes a new instance of the <see cref="DomainFieldsScenario"/> class.</summary>
    /// <param name="zone">The invented zone, letters only.</param>
    /// <param name="host">The host the fakes are installed in.</param>
    /// <param name="admin">An admin client against the host.</param>
    /// <param name="registrar">The registrar every operation of the zone reaches.</param>
    private DomainFieldsScenario(string zone, WebApplicationFactory<Program> host, HttpClient admin, RecordingRegistrar registrar)
    {
        Zone = zone;
        Host = host;
        Admin = admin;
        Registrar = registrar;
    }

    /// <summary>Gets the invented zone the scenario's TLD is configured for.</summary>
    public string Zone { get; }

    /// <summary>Gets the host the fake registrar, catalogue and TLD configuration are installed in.</summary>
    public WebApplicationFactory<Program> Host { get; }

    /// <summary>Gets an admin client against <see cref="Host"/>.</summary>
    public HttpClient Admin { get; }

    /// <summary>Gets the registrar that remembers every order and contact change it was asked for.</summary>
    internal RecordingRegistrar Registrar { get; }

    /// <summary>Starts a host with a zone configured for a registrar whose manifest declares the given sets.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="sets">Builds the manifest's sets of domain fields for the invented zone.</param>
    /// <param name="locales">The manifest's locale texts by language, or <see langword="null"/> for none.</param>
    /// <param name="withPrices">Whether the zone has a sell price, so a checkout can order a domain under it.</param>
    /// <returns>The scenario.</returns>
    internal static async Task<DomainFieldsScenario> StartAsync(
        IntegrationTestFactory factory,
        Func<string, IReadOnlyList<PluginDomainFieldSet>> sets,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? locales = null,
        bool withPrices = false)
    {
        var zone = DomainsTestSeeds.UniqueTld();
        if (withPrices)
        {
            await OrdersTestSeeds.SeedUsdTldAndRatesAsync(factory, zone, PricedZoneSellPrice);
        }
        else
        {
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            context.TldConfigs.Add(TldConfig.Create(zone, TestRegistrarModules.First, "USD", "USD", true, 1, []));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var registrar = new RecordingRegistrar(new ManualTimeProvider(TestMoments.Seeded));
        var registrars = new FakeRegistrarProviderFactory();
        registrars.Register(TestRegistrarModules.First, registrar);
        var catalogue = new FakeRegistrarDomainFieldCatalogue();
        catalogue.Add(
            TestRegistrarModules.First,
            FakeRegistrarDomainFieldCatalogue.Manifest(TestRegistrarModules.First, sets(zone), locales));

        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IRegistrarProviderFactory>(registrars);
                services.AddSingleton<IRegistrarDomainFieldCatalogue>(catalogue);
            }));

        var admin = host.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());
        return new DomainFieldsScenario(zone, host, admin, registrar);
    }

    /// <summary>Builds a domain name under the invented zone that no other test uses.</summary>
    /// <returns>The fully qualified name.</returns>
    public string NewDomainName()
    {
        return $"{Guid.NewGuid():N}"[..12] + $".{Zone}";
    }
}
