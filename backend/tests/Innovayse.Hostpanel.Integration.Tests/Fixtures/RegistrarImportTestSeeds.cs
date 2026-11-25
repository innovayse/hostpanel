using System.Net.Http.Headers;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Registrar;
using Hostpanel.Sdk.Plugins.Results;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Shared setup for the registrar domain import tests: a registrar plugin that lists the domains its account holds, the real
/// <see cref="PluginRegistrarProvider"/> over it, and an admin client against a host that resolves that provider for the first
/// test registrar module. Only the plugin, which is not ours, is scripted.
/// </summary>
internal static class RegistrarImportTestSeeds
{
    /// <summary>Builds a plugin whose domain list answers from the given pages.</summary>
    /// <param name="pages">The page the registrar answers for a cursor (null for the first page).</param>
    /// <returns>The scripted plugin.</returns>
    public static FullRegistrarPlugin PluginListing(Func<string?, RegistrarDomainPage> pages)
    {
        return new FullRegistrarPlugin(new ConfigurationBuilder().Build())
        {
            DomainPages = cursor => ValueResult<RegistrarDomainPage, RegistrarRefusalReason>.Done(pages(cursor)),
        };
    }

    /// <summary>Builds a plugin that lists exactly one page of domains.</summary>
    /// <param name="domains">The domains the account holds.</param>
    /// <returns>The scripted plugin.</returns>
    public static FullRegistrarPlugin PluginHolding(params RegistrarListedDomain[] domains)
    {
        return PluginListing(_ => new RegistrarDomainPage(domains, null));
    }

    /// <summary>Names a domain the way the registrar's list does.</summary>
    /// <param name="name">The domain name.</param>
    /// <param name="registrarRef">The registrar's reference.</param>
    /// <param name="expiresAt">The expiry the registrar reports, or <see langword="null"/>.</param>
    /// <returns>The listed domain.</returns>
    public static RegistrarListedDomain Listed(string name, string registrarRef, DateTimeOffset? expiresAt)
    {
        return new RegistrarListedDomain(new RegistrarDomain(name, registrarRef), expiresAt);
    }

    /// <summary>An admin client against a host where the first test registrar module resolves to the real provider over the plugin.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="plugin">The plugin the registrar module is served by.</param>
    /// <returns>An authenticated admin client.</returns>
    public static async Task<HttpClient> AdminClientAsync(IntegrationTestFactory factory, FullRegistrarPlugin plugin)
    {
        var manifest = PluginCapabilitySeeds.Plugin(
            TestRegistrarModules.First, PluginType.Registrar, plugin.GetType(),
            PluginCapabilities.RegistrarOperations, PluginCapabilities.RegistrarDomainList).Manifest;
        var registrars = new FakeRegistrarProviderFactory();
        registrars.Register(
            TestRegistrarModules.First,
            new PluginRegistrarProvider(
                manifest, plugin, new RegistrarConnection("account", "key", null, null, false), NullLogger<PluginRegistrarProvider>.Instance));

        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IRegistrarProviderFactory>(registrars);
                services.AddSingleton<IRegistrarModuleCatalogue>(new FakeRegistrarModuleCatalogue(installed: [TestRegistrarModules.First]));
            }));

        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());
        return client;
    }
}
