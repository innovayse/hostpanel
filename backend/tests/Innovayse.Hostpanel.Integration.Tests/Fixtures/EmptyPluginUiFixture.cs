using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>A host whose plugin registry is empty, built once for a test class, for the lists that must be empty.</summary>
public sealed class EmptyPluginUiFixture : IAsyncLifetime
{
    /// <summary>Gets the shared API and database the host is derived from.</summary>
    public IntegrationTestFactory Factory { get; } = new();

    /// <summary>Gets the host with no plugin loaded.</summary>
    public WebApplicationFactory<Program> Host { get; private set; } = null!;

    /// <summary>Builds the factory, the database and the derived host with an empty registry.</summary>
    /// <returns>A task that completes when the host is ready.</returns>
    public async Task InitializeAsync()
    {
        await Factory.InitializeAsync();

        var registry = PluginActivationTestSeeds.Registry();
        Host = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<PluginRegistry>();
                services.AddSingleton(registry);
            }));
    }

    /// <summary>Releases the host and the factory.</summary>
    /// <returns>A task that completes when both are released.</returns>
    public async Task DisposeAsync()
    {
        Host.Dispose();
        await Factory.DisposeAsync();
    }
}
