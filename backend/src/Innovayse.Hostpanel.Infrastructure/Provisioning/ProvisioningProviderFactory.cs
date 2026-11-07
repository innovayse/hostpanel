using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm.Models;
using Innovayse.Hostpanel.Infrastructure.Integrations.Plesk;
using Innovayse.Hostpanel.Infrastructure.Integrations.Plesk.Models;
using Innovayse.Hostpanel.Infrastructure.Provisioning.Settings;
using Innovayse.Hostpanel.Infrastructure.Resilience;
using Innovayse.Providers.CWP7;
using Innovayse.Providers.CWP7.Models;
using Hostpanel.Sdk.Plugins.HostingPanel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Provisioning;

/// <summary>
/// Creates <see cref="IProvisioningProvider"/> instances configured for a specific server.
/// Resolves the correct provider implementation based on the server's <see cref="ServerConnectionDto.Module"/>.
/// </summary>
public sealed class ProvisioningProviderFactory : IProvisioningProviderFactory
{
    /// <summary>
    /// The modules <see cref="CreateFor"/> builds a real provider for; extend it with the switch there.
    /// Read-only, so no caller can change the process-wide list; test doubles reference it rather
    /// than copy it.
    /// </summary>
    public static IReadOnlyList<ServerModule> BuiltModules { get; } = Array.AsReadOnly<ServerModule>([ServerModule.Cwp7, ServerModule.Plesk, ServerModule.CPanel]);

    /// <summary>Every module, which the development fake provider stands in for.</summary>
    private static readonly IReadOnlyList<ServerModule> AllModules = Array.AsReadOnly(Enum.GetValues<ServerModule>());

    /// <summary>Factory for creating HTTP clients for provider API calls.</summary>
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>Factory for creating typed loggers for each provider.</summary>
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>Provisioning behaviour switches, notably the development fake-provider flag and the CWP7 ports.</summary>
    private readonly IOptions<ProvisioningOptions> _options;

    /// <summary>Initializes a new instance of the <see cref="ProvisioningProviderFactory"/> class.</summary>
    /// <param name="httpClientFactory">Factory for creating HTTP clients for provider API calls.</param>
    /// <param name="loggerFactory">Factory for creating typed loggers for each provider.</param>
    /// <param name="options">Provisioning behaviour switches, notably the development fake-provider flag and the CWP7 ports.</param>
    public ProvisioningProviderFactory(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, IOptions<ProvisioningOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
        _options = options;
    }

    /// <inheritdoc/>
    /// <remarks>With the development fake provider on, every module: that provider answers for any server.</remarks>
    public IReadOnlyCollection<ServerModule> SupportedModules => _options.Value.UseFakeProvider ? AllModules : BuiltModules;

    /// <summary>
    /// Creates a provisioning provider configured with the credentials of the given server.
    /// </summary>
    /// <param name="server">The server to create the provider for.</param>
    /// <returns>A configured <see cref="IProvisioningProvider"/>.</returns>
    /// <exception cref="NotSupportedException">Thrown for unsupported server modules.</exception>
    /// <exception cref="InvalidOperationException">Thrown when required credentials are missing.</exception>
    public IProvisioningProvider CreateFor(ServerConnectionDto server)
    {
        // Development short-circuit: with no live control panel to call, return the in-process
        // no-op provider so the setup flow completes. Guarded by config that defaults off, so a
        // real environment never reaches this and always builds a provider that provisions for
        // real. Server selection still ran before this, so the flow is exercised end to end bar
        // the outbound HTTP call itself.
        if (_options.Value.UseFakeProvider)
        {
            return new NullCPanelProvisioningProvider(
                _loggerFactory.CreateLogger<NullCPanelProvisioningProvider>());
        }

        return server.Module switch
        {
            ServerModule.Cwp7 => CreateCwp7Provider(server),
            ServerModule.Plesk => CreatePleskProvider(server),
            ServerModule.CPanel => CreateCPanelProvider(server),
            _ => throw new NotSupportedException($"No provisioning provider for module '{server.Module}'. Only CWP7, Plesk and cPanel are currently supported via the factory."),
        };
    }

    /// <summary>Creates a cPanel provider for the given server, talking to WHM API 1.</summary>
    /// <param name="server">The cPanel server; its username and API token are the WHM user and its token.</param>
    /// <returns>A configured <see cref="WhmProvisioningProvider"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the server has no WHM username or no API token.</exception>
    private WhmProvisioningProvider CreateCPanelProvider(ServerConnectionDto server)
    {
        if (string.IsNullOrWhiteSpace(server.Username) || string.IsNullOrWhiteSpace(server.ApiToken))
        {
            throw new InvalidOperationException($"Server '{server.Name}' (id={server.Id}) has no WHM username or API token configured.");
        }

        var options = _options.Value;
#pragma warning disable CS0618 // Legacy HostingPanel contract (the CWP clients and HostingPanelAddress, which the Plesk and cPanel paths also use), obsolete since SDK 3.1.0; stays until the panel providers become IProvisioningPanelPlugin plugins.
        var connection = new WhmConnection(HostingPanelAddress.BaseUrl(server.Hostname, options.CPanelWhmPort), server.Username, server.ApiToken);
        var panelUrl = HostingPanelAddress.BaseUrl(server.Hostname, options.CPanelUserPort);
#pragma warning restore CS0618
        var client = new WhmApiClient(
            _httpClientFactory.CreateClient(HttpClientNames.CPanelWhm),
            _loggerFactory.CreateLogger<WhmApiClient>());

        return new WhmProvisioningProvider(
            connection,
            server.IpAddress ?? server.Hostname,
            panelUrl,
            client,
            _loggerFactory.CreateLogger<WhmProvisioningProvider>());
    }

    /// <summary>Creates a Plesk provider for the given server.</summary>
    /// <param name="server">The Plesk server; its access hash holds the Plesk API key.</param>
    /// <returns>A configured <see cref="PleskProvisioningProvider"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the server has no usable API key: null, empty or whitespace.
    /// </exception>
    private PleskProvisioningProvider CreatePleskProvider(ServerConnectionDto server)
    {
        var apiKey = server.AccessHash;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"Server '{server.Name}' (id={server.Id}) has no access hash configured.");
        }

#pragma warning disable CS0618 // Legacy HostingPanel contract (the CWP clients and HostingPanelAddress, which the Plesk and cPanel paths also use), obsolete since SDK 3.1.0; stays until the panel providers become IProvisioningPanelPlugin plugins.
        var connection = new PleskConnection(HostingPanelAddress.BaseUrl(server.Hostname, _options.Value.PleskPort), apiKey);
#pragma warning restore CS0618
        var client = new PleskApiClient(
            _httpClientFactory.CreateClient(HttpClientNames.Plesk),
            _loggerFactory.CreateLogger<PleskApiClient>());

        return new PleskProvisioningProvider(
            connection, server.IpAddress, client,
            _loggerFactory.CreateLogger<PleskProvisioningProvider>());
    }

    /// <summary>
    /// Creates a CWP7 provider for the given server.
    /// </summary>
    /// <param name="server">The CWP7 server.</param>
    /// <returns>A configured <see cref="Cwp7ProvisioningProvider"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the server has no usable access hash: null, empty or whitespace.
    /// </exception>
    private Cwp7ProvisioningProvider CreateCwp7Provider(ServerConnectionDto server)
    {
        // Whitespace, not just null: a server row saved with the access-hash field cleared holds
        // an empty string, and that used to build a provider with an empty API key that failed
        // later, at the provider, instead of here with the message this line already writes.
        var apiKey = server.AccessHash;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"Server '{server.Name}' (id={server.Id}) has no access hash configured.");
        }

        var options = _options.Value;
#pragma warning disable CS0618 // Legacy HostingPanel contract (the CWP clients and HostingPanelAddress, which the Plesk and cPanel paths also use), obsolete since SDK 3.1.0; stays until the panel providers become IProvisioningPanelPlugin plugins.
        var connection = new Cwp7Connection(
            HostingPanelAddress.BaseUrl(server.Hostname, options.Cwp7ApiPort),
            apiKey,
            HostingPanelAddress.BaseUrl(server.Hostname, options.Cwp7UserPanelPort));
#pragma warning restore CS0618
        var serverIp = server.IpAddress ?? server.Hostname;

        var httpClient = _httpClientFactory.CreateClient(HttpClientNames.Cwp7);

        var client = new Cwp7ApiClient(httpClient, _loggerFactory.CreateLogger<Cwp7ApiClient>());

        return new Cwp7ProvisioningProvider(
            connection, serverIp, client,
            _loggerFactory.CreateLogger<Cwp7ProvisioningProvider>());
    }
}
