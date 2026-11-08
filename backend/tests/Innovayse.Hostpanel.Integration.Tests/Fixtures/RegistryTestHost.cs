using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Plugins.Registry;
using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// The API host wired to a <see cref="FakeRegistryServer"/>: the host's own registry client and registration client run unchanged,
/// only the network under them is the fake server. The registry is switched on and points at the fake address, the public address
/// of the installation is a name the registry accepts, and the fake registry reads the challenge by calling the host's own
/// well-known route, as the real registry calls the public address.
/// </summary>
internal sealed class RegistryTestHost : IDisposable
{
    /// <summary>The public address the installation tells the registry, unless a test names another.</summary>
    public const string PublicUrl = "https://panel.example.test";

    /// <summary>The derived host.</summary>
    private readonly WebApplicationFactory<Program> _host;

    /// <summary>Initializes the host.</summary>
    /// <param name="factory">The shared API + PostgreSQL host the derived host comes from.</param>
    /// <param name="registry">The fake registry the host talks to.</param>
    /// <param name="publicUrl">The configured public address; <see cref="PublicUrl"/> when <see langword="null"/>.</param>
    /// <param name="clock">A clock for the host to read; the system clock when <see langword="null"/>.</param>
    /// <param name="enabled">Whether the registry is switched on.</param>
    public RegistryTestHost(
        IntegrationTestFactory factory,
        FakeRegistryServer registry,
        string? publicUrl = null,
        TimeProvider? clock = null,
        bool enabled = true)
    {
        Registry = registry;
        _host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                // Options, not host settings: appsettings.json names the PluginRegistry section and would win over a host setting.
                services.PostConfigure<PluginRegistryOptions>(o =>
                {
                    o.Enabled = enabled;
                    o.BaseUrl = FakeRegistryServer.BaseUrl;
                });
                services.PostConfigure<ClientPortalOptions>(o => o.BaseUrl = publicUrl ?? PublicUrl);

                // The real clients with the fake server as their network.
                services.RemoveAll<IPluginRegistryClient>();
                services.AddHttpClient<IPluginRegistryClient, PluginRegistryClient>().ConfigurePrimaryHttpMessageHandler(() => registry);
                services.RemoveAll<IRegistryInstanceApi>();
                services.AddHttpClient<IRegistryInstanceApi, RegistryInstanceApi>().ConfigurePrimaryHttpMessageHandler(() => registry);

                // Packages are signed by the test key; the signature rule still applies to a registry install.
                services.RemoveAll<PluginSignatureVerifier>();
                services.AddSingleton(PluginSigningTestSeeds.Verifier(enforced: false));
                if (clock is not null)
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton(clock);
                }
            });
        });

        // The tests of a class share one database, and each plays its own registry: the identity an earlier test left behind
        // belongs to a registry that no longer exists, so every host starts without one.
        using (var scope = _host.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Settings
                .Where(setting => setting.Key == PluginSettingKeys.RegistryInstance)
                .ExecuteDelete();
        }

        // The registry reads the challenge from the address of the installation; in-process that is the host's own route.
        var reader = _host.CreateClient();
        registry.FetchChallenge = async id =>
        {
            var response = await reader.GetAsync($"/.well-known/hostpanel-registry/{id:D}");
            return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync() : null;
        };
    }

    /// <summary>Gets the fake registry the host talks to.</summary>
    public FakeRegistryServer Registry { get; }

    /// <summary>Gets the services of the host.</summary>
    public IServiceProvider Services => _host.Services;

    /// <summary>Creates an anonymous client for the host.</summary>
    /// <returns>The client.</returns>
    public HttpClient CreateClient()
    {
        return _host.CreateClient();
    }

    /// <summary>Creates a client signed in as an administrator.</summary>
    /// <param name="factory">The shared host, which mints the token.</param>
    /// <returns>The authenticated client.</returns>
    public Task<HttpClient> AdminClientAsync(IntegrationTestFactory factory)
    {
        return PluginsTestSeeds.AdminClientAsync(factory, _host);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _host.Dispose();
    }
}
