using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.HostingPanel;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Provisioning;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Common.Enums;
using Innovayse.Hostpanel.Application.Admin.Servers.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm.Models;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Provisioning.Settings;
using Innovayse.Hostpanel.Infrastructure.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Provisioning;

/// <summary>
/// Tests server connectivity: through the installed panel plugin that serves the server's module when there is one, otherwise
/// with the API client the host has for the module.
/// </summary>
public sealed class ServerConnectionTester : IServerConnectionTester
{
#pragma warning disable CS0618 // Legacy HostingPanel contract (the CWP client and HostingPanelAddress, which the cPanel path also uses), obsolete since SDK 3.1.0; stays until a cPanel plugin and a legacy CWP plugin exist.
    /// <summary>The CWP API client, for servers on the CWP module.</summary>
    private readonly IHostingPanelApiClient _cwpClient;
#pragma warning restore CS0618

    /// <summary>Builds the named WHM HTTP client, so the WHM timeout and resilience profile apply.</summary>
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>Builds the WHM client's logger.</summary>
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>The port cPanel servers answer WHM on.</summary>
    private readonly IOptions<ProvisioningOptions> _options;

    /// <summary>Records why a connection test failed, detail the caller never sees.</summary>
    private readonly ILogger<ServerConnectionTester> _logger;

    /// <summary>Finds the installed plugin that serves a server module.</summary>
    private readonly IServerModulePluginResolver _resolver;

    /// <summary>Activates a panel plugin and runs its connection test.</summary>
    private readonly PanelPluginCaller _caller;

#pragma warning disable CS0618 // Legacy HostingPanel contract (the CWP client and HostingPanelAddress, which the cPanel path also uses), obsolete since SDK 3.1.0; stays until a cPanel plugin and a legacy CWP plugin exist.
    /// <summary>Initializes a new instance of the <see cref="ServerConnectionTester"/> class.</summary>
    /// <param name="cwpClient">The CWP API client, for servers on the CWP module.</param>
    /// <param name="httpClientFactory">Builds the named WHM HTTP client, so the WHM timeout and resilience profile apply.</param>
    /// <param name="loggerFactory">Builds the WHM client's logger.</param>
    /// <param name="options">The port cPanel servers answer WHM on.</param>
    /// <param name="logger">Records why a connection test failed, detail the caller never sees.</param>
    /// <param name="resolver">Finds the installed plugin that serves a server module.</param>
    /// <param name="caller">Activates a panel plugin and runs its connection test.</param>
    public ServerConnectionTester(
        IHostingPanelApiClient cwpClient,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        IOptions<ProvisioningOptions> options,
        ILogger<ServerConnectionTester> logger,
        IServerModulePluginResolver resolver,
        PanelPluginCaller caller)
#pragma warning restore CS0618
    {
        _cwpClient = cwpClient;
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
        _options = options;
        _logger = logger;
        _resolver = resolver;
        _caller = caller;
    }

    /// <summary>
    /// Tests connectivity to the server using its stored credentials.
    /// A module an installed plugin serves is tested through the plugin; otherwise cPanel and legacy CentOS are tested with the
    /// host's own clients, and any other module answers <c>PANEL_PLUGIN_UNAVAILABLE</c>, as every provisioning call does.
    /// </summary>
    /// <param name="server">The server to test.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The connection result.</returns>
    public async Task<ServerConnectionTestResult> TestAsync(Server server, CancellationToken ct)
    {
        var match = _resolver.Find(server.Module, PluginCapabilities.ProvisioningPanel);
        if (match.Outcome is ServerModulePluginOutcome.Served)
        {
            return await TestPluginAsync(match.PluginId!, server, ct);
        }

        if (match.Outcome is ServerModulePluginOutcome.Conflict)
        {
            return ServerConnectionTestResult.Failed(HostpanelErrorCodes.PANEL_PLUGIN_CONFLICT);
        }

        if (ServerModules.Same(server.Module, ServerModules.CPanel))
        {
            return await TestCPanelAsync(server, ct);
        }

        return ServerModules.Same(server.Module, ServerModules.Centos)
            ? await TestLegacyCwpAsync(server, ct)
            : ServerConnectionTestResult.Failed(HostpanelErrorCodes.PANEL_PLUGIN_UNAVAILABLE);
    }

    /// <summary>Tests connectivity to a server through the panel plugin that serves its module.</summary>
    /// <param name="pluginId">The serving plugin.</param>
    /// <param name="server">The server to test; its stored password goes to the plugin with the other connection details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The connection result; a plugin that is off, unconfigured or throwing is a failed test, never an exception.</returns>
    private async Task<ServerConnectionTestResult> TestPluginAsync(string pluginId, Server server, CancellationToken ct)
    {
        var context = new PanelTestConnectionContext(server.ToConnection().ToPanelConnection(server.Password));
        try
        {
            var answer = await _caller.RunAsync<IProvisioningPanelPlugin, PanelConnectionTestResult>(
                pluginId, nameof(IProvisioningPanelPlugin.TestConnectionAsync), (plugin, token) => plugin.TestConnectionAsync(context, token), ct);

            return answer.Outcome switch
            {
                PanelConnectionOutcome.Connected => ServerConnectionTestResult.Succeeded(answer.AccountCount, answer.Version),
                PanelConnectionOutcome.NotConfigured => ServerConnectionTestResult.Failed(HostpanelErrorCodes.SERVER_CONNECTION_NOT_CONFIGURED),
                PanelConnectionOutcome.AuthenticationFailed => ServerConnectionTestResult.Failed(HostpanelErrorCodes.SERVER_CONNECTION_AUTH_FAILED),
                _ => ServerConnectionTestResult.Failed(HostpanelErrorCodes.SERVER_CONNECTION_UNREACHABLE),
            };
        }
        catch (DomainRuleException ex) when (ex.Code is HostpanelErrorCodes.PANEL_PLUGIN_UNAVAILABLE)
        {
            return ServerConnectionTestResult.Failed(ex.Code);
        }
        catch (PanelPluginCallException)
        {
            // The caller logged the plugin and the exception type; the plugin's own text never reaches the administrator.
            return ServerConnectionTestResult.Failed(HostpanelErrorCodes.SERVER_CONNECTION_UNREACHABLE);
        }
    }

    /// <summary>Tests connectivity to a cPanel server by reading the WHM version and the account count with its API token.</summary>
    /// <param name="server">The cPanel server; its username and API token are the WHM user and its token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The connection result.</returns>
    private async Task<ServerConnectionTestResult> TestCPanelAsync(Server server, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(server.Username) || string.IsNullOrWhiteSpace(server.ApiToken))
        {
            return ServerConnectionTestResult.Failed(HostpanelErrorCodes.SERVER_CONNECTION_NO_API_KEY);
        }

        var client = new WhmApiClient(
            _httpClientFactory.CreateClient(HttpClientNames.CPanelWhm),
            _loggerFactory.CreateLogger<WhmApiClient>());
        try
        {
            // Inside the try: an address that cannot be turned into a URL is an unreachable server.
#pragma warning disable CS0618 // Legacy HostingPanel contract (the CWP client and HostingPanelAddress, which the cPanel path also uses), obsolete since SDK 3.1.0; stays until a cPanel plugin and a legacy CWP plugin exist.
            var connection = new WhmConnection(HostingPanelAddress.BaseUrl(server.Hostname, _options.Value.CPanelWhmPort), server.Username, server.ApiToken);
#pragma warning restore CS0618
            var info = await client.GetVersionAsync(connection, ct);
            var accounts = await client.CountAccountsAsync(connection, ct);
            return ServerConnectionTestResult.Succeeded(accounts, info.Version);
        }
        catch (Exception ex)
        {
            // The exception type only, as WhmApiClient logs: an exception's text could name the
            // request, and WHM's own text never reaches the caller either.
            _logger.LogWarning("cPanel connection test failed for server {ServerId} ({Hostname}) ({ErrorType}).", server.Id, server.Hostname, ex.GetType().Name);
            return ServerConnectionTestResult.Failed(HostpanelErrorCodes.SERVER_CONNECTION_UNREACHABLE);
        }
    }

    /// <summary>
    /// Tests connectivity to a legacy CentOS Web Panel server using IHostingPanelApiClient.
    /// </summary>
    /// <param name="server">The legacy CWP server.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The connection result.</returns>
    private async Task<ServerConnectionTestResult> TestLegacyCwpAsync(Server server, CancellationToken ct)
    {
        var apiKey = server.AccessHash ?? server.ApiToken;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return ServerConnectionTestResult.Failed(HostpanelErrorCodes.SERVER_CONNECTION_NO_API_KEY);
        }

        try
        {
            var info = await _cwpClient.GetServerInfoAsync(ConnectionTo(server, apiKey), ct);
            return ServerConnectionTestResult.Succeeded(info.AccountsCount, info.Version);
        }
        catch (Exception ex)
        {
            // The provider's own message never reaches the caller (rules on upstream detail); it
            // is logged here for whoever has to chase the failure up.
            _logger.LogWarning(ex, "CWP connection test failed for server {ServerId} ({Hostname})", server.Id, server.Hostname);
            return ServerConnectionTestResult.Failed(HostpanelErrorCodes.SERVER_CONNECTION_UNREACHABLE);
        }
    }

#pragma warning disable CS0618 // Legacy HostingPanel contract (the CWP client and HostingPanelAddress, which the cPanel path also uses), obsolete since SDK 3.1.0; stays until a cPanel plugin and a legacy CWP plugin exist.
    /// <summary>Builds the panel connection for a server on the panel's default API port.</summary>
    /// <param name="server">The server to reach.</param>
    /// <param name="apiKey">The credential the server stores for its panel API.</param>
    /// <returns>The connection to hand the panel client.</returns>
    private static HostingPanelConnection ConnectionTo(Server server, string apiKey) =>
        new(server.Hostname, IHostingPanelApiClient.DefaultApiPort, apiKey);
#pragma warning restore CS0618
}
