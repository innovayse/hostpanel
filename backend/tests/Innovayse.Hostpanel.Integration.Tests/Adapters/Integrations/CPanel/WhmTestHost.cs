using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm.Extensions;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm.Models;
using Innovayse.Hostpanel.Infrastructure.Resilience;
using Innovayse.Hostpanel.Infrastructure.Resilience.Settings;
using Innovayse.Hostpanel.Infrastructure.Security;
using Innovayse.Hostpanel.Infrastructure.Security.Settings;
using Innovayse.Hostpanel.Integration.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.CPanel;

/// <summary>
/// Builds the WHM HTTP client through <see cref="CPanelWhmHttpClientExtensions.AddCPanelWhmHttpClient"/>,
/// the same call the application makes, so a test describes the registration that really runs
/// (request logging, redirects, address guard, resilience profile) and not a copy of it.
/// </summary>
internal sealed class WhmTestHost : IAsyncDisposable
{
    /// <summary>The name the stub server is reached by; the panel route sends it to loopback, which the address guard would otherwise refuse.</summary>
    public const string PanelHost = "whm.invalid";

    /// <summary>The built container.</summary>
    private readonly ServiceProvider _provider;

    /// <summary>Initializes a new instance of the <see cref="WhmTestHost"/> class.</summary>
    /// <param name="provider">The built container.</param>
    /// <param name="logs">Every line logged through the container.</param>
    private WhmTestHost(ServiceProvider provider, RecordingLoggerProvider logs)
    {
        _provider = provider;
        Logs = logs;
    }

    /// <summary>Gets every line logged through the container, by any category.</summary>
    public RecordingLoggerProvider Logs { get; }

    /// <summary>Builds a host with the real WHM client registration.</summary>
    /// <param name="configureResilience">Changes the resilience numbers (for example a short timeout), or null to keep the defaults.</param>
    /// <returns>The host.</returns>
    public static WhmTestHost Build(Action<HttpResilienceOptions>? configureResilience = null)
    {
        var logs = new RecordingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        services.AddOptions<HttpResilienceOptions>().Configure(o => configureResilience?.Invoke(o));
        services.AddSingleton(Options.Create(new OutboundNetworkOptions()));
        services.AddSingleton(Options.Create(new PanelNetworkOptions { HostAddresses = $"{PanelHost}=127.0.0.1" }));
        services.AddSingleton<OutboundConnector>();
        services.AddCPanelWhmHttpClient();
        return new WhmTestHost(services.BuildServiceProvider(), logs);
    }

    /// <summary>Resolves the named client the way the provider factory does.</summary>
    /// <returns>The WHM API client.</returns>
    public WhmApiClient Client() => new(
        _provider.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientNames.CPanelWhm),
        _provider.GetRequiredService<ILoggerFactory>().CreateLogger<WhmApiClient>());

    /// <summary>The connection that reaches a stub server through the routed panel host name.</summary>
    /// <param name="server">The stub standing in for WHM.</param>
    /// <returns>The connection.</returns>
    public static WhmConnection ConnectionTo(StubHttpServer server) =>
        new($"http://{PanelHost}:{server.BaseAddress.Port}", "root", "api-token-value");

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
