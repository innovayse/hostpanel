using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.HostingPanel;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Provisioning;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm.Models;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;
using Innovayse.Hostpanel.Infrastructure.Provisioning.Settings;
using Innovayse.Hostpanel.Infrastructure.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Provisioning;

/// <summary>
/// Creates <see cref="IProvisioningProvider"/> instances configured for a specific server.
/// Resolves the correct provider implementation based on the server's <see cref="ServerConnectionDto.Module"/>: the installed
/// panel plugin that serves the module when there is one, otherwise the host's own provider for it.
/// </summary>
public sealed class ProvisioningProviderFactory : IProvisioningProviderFactory
{
    /// <summary>
    /// The modules <see cref="CreateFor"/> builds a real provider for; extend it with the switch there.
    /// Read-only, so no caller can change the process-wide list; test doubles reference it rather
    /// than copy it.
    /// </summary>
    public static IReadOnlyList<string> BuiltModules { get; } = ServerModules.Known;

    /// <summary>Factory for creating HTTP clients for provider API calls.</summary>
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>Factory for creating typed loggers for each provider.</summary>
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>Provisioning behaviour switches, notably the development fake-provider flag and the cPanel ports.</summary>
    private readonly IOptions<ProvisioningOptions> _options;

    /// <summary>Finds the installed plugin that serves a server module.</summary>
    private readonly IServerModulePluginResolver _resolver;

    /// <summary>Registry of loaded plugins, read for the panel plugins a new order may use.</summary>
    private readonly PluginRegistry _registry;

    /// <summary>Tells which panel plugins are switched on and configured.</summary>
    private readonly PluginActivator _activator;

    /// <summary>Runs the calls of a panel plugin provider.</summary>
    private readonly PanelPluginCaller _caller;

    /// <summary>Builds the connection a panel plugin receives.</summary>
    private readonly PanelConnectionReader _connections;

    /// <summary>Gives a plugin the opaque reference it sees for a client.</summary>
    private readonly IPluginReferences _references;

    /// <summary>Initializes a new instance of the <see cref="ProvisioningProviderFactory"/> class.</summary>
    /// <param name="httpClientFactory">Factory for creating HTTP clients for provider API calls.</param>
    /// <param name="loggerFactory">Factory for creating typed loggers for each provider.</param>
    /// <param name="options">Provisioning behaviour switches, notably the development fake-provider flag and the cPanel ports.</param>
    /// <param name="resolver">Finds the installed plugin that serves a server module.</param>
    /// <param name="registry">Registry of loaded plugins, read for the panel plugins a new order may use.</param>
    /// <param name="activator">Tells which panel plugins are switched on and configured.</param>
    /// <param name="caller">Runs the calls of a panel plugin provider.</param>
    /// <param name="connections">Builds the connection a panel plugin receives.</param>
    /// <param name="references">Gives a plugin the opaque reference it sees for a client.</param>
    public ProvisioningProviderFactory(
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        IOptions<ProvisioningOptions> options,
        IServerModulePluginResolver resolver,
        PluginRegistry registry,
        PluginActivator activator,
        PanelPluginCaller caller,
        PanelConnectionReader connections,
        IPluginReferences references)
    {
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
        _options = options;
        _resolver = resolver;
        _registry = registry;
        _activator = activator;
        _caller = caller;
        _connections = connections;
        _references = references;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// With the development fake provider on, every module: that provider answers for any server. Otherwise the host's own panels
    /// plus the modules of the panel plugins that activate now (switched on and configured): an order is never placed on a server
    /// whose plugin would only refuse it.
    /// </remarks>
    public async Task<IReadOnlyCollection<string>> SupportedModulesAsync(CancellationToken ct)
    {
        if (_options.Value.UseFakeProvider)
        {
            return ServerModules.Known;
        }

        var modules = new List<string>(BuiltModules);
        foreach (var plugin in _registry.OfType(PluginType.Provisioning).Where(p => p.Manifest.Declares(PluginCapabilities.ProvisioningPanel)))
        {
            var prepared = await _activator.PrepareAsync<IProvisioningPanelPlugin>(plugin, ct);
            if (prepared.Outcome is PluginActivationOutcome.Activated)
            {
                foreach (var id in plugin.Manifest.ServerModules.Where(id => !modules.Any(known => ServerModules.Same(known, id))).ToList())
                {
                    modules.Add(id);
                }
            }
        }

        return modules;
    }

    /// <summary>
    /// Creates a provisioning provider configured with the credentials of the given server.
    /// </summary>
    /// <param name="server">The server to create the provider for.</param>
    /// <returns>A configured <see cref="IProvisioningProvider"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required credentials are missing.</exception>
    /// <exception cref="DomainRuleException">
    /// Thrown with <c>PANEL_PLUGIN_CONFLICT</c> when two installed plugins serve the module, and with <c>PANEL_PLUGIN_UNAVAILABLE</c> when
    /// neither a plugin nor the host serves it.
    /// </exception>
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

        // An installed plugin that owns the module wins over a built in provider: the same panel moving to a plugin
        // keeps its stored servers, services and references.
        var match = _resolver.Find(server.Module, PluginCapabilities.ProvisioningPanel);
        switch (match.Outcome)
        {
            case ServerModulePluginOutcome.Served:
                return new PanelPluginProvisioningProvider(
                    match.PluginId!, server, _caller, _connections, _references,
                    _loggerFactory.CreateLogger<PanelPluginProvisioningProvider>());
            case ServerModulePluginOutcome.Conflict:
                throw new DomainRuleException(
                    HostpanelErrorCodes.PANEL_PLUGIN_CONFLICT, $"More than one installed plugin serves the module '{server.Module}'.");
        }

        if (ServerModules.Same(server.Module, ServerModules.CPanel))
        {
            return CreateCPanelProvider(server);
        }

        // Only cPanel is built in: a module no installed plugin serves is a panel that is not available, answered like a plugin that is off.
        throw new DomainRuleException(
            HostpanelErrorCodes.PANEL_PLUGIN_UNAVAILABLE, $"No installed plugin serves the module '{server.Module}', and only cPanel is built in.");
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
#pragma warning disable CS0618 // Legacy HostingPanel contract (HostingPanelAddress, which the cPanel path uses), obsolete since SDK 3.1.0; stays until a cPanel plugin exists.
        // WHM is dialled at ApiEndpointHost (the hostname unless the server carries an ApiHost); the
        // user panel URL stays on the hostname, since it is handed to clients.
        var connection = new WhmConnection(HostingPanelAddress.BaseUrl(server.ApiEndpointHost, options.CPanelWhmPort), server.Username, server.ApiToken);
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
}
