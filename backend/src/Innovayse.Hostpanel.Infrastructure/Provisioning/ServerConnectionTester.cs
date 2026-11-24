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
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.Hostpanel.Infrastructure.Provisioning.Settings;
using Innovayse.Hostpanel.Infrastructure.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Provisioning;

/// <summary>
/// Tests server connectivity: through the installed panel plugin that serves the server's module when there is one, otherwise
/// with the host's own cPanel client.
/// </summary>
public sealed class ServerConnectionTester : IServerConnectionTester
{
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

    /// <summary>Initializes a new instance of the <see cref="ServerConnectionTester"/> class.</summary>
    /// <param name="httpClientFactory">Builds the named WHM HTTP client, so the WHM timeout and resilience profile apply.</param>
    /// <param name="loggerFactory">Builds the WHM client's logger.</param>
    /// <param name="options">The port cPanel servers answer WHM on.</param>
    /// <param name="logger">Records why a connection test failed, detail the caller never sees.</param>
    /// <param name="resolver">Finds the installed plugin that serves a server module.</param>
    /// <param name="caller">Activates a panel plugin and runs its connection test.</param>
    public ServerConnectionTester(
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        IOptions<ProvisioningOptions> options,
        ILogger<ServerConnectionTester> logger,
        IServerModulePluginResolver resolver,
        PanelPluginCaller caller)
    {
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
        _options = options;
        _logger = logger;
        _resolver = resolver;
        _caller = caller;
    }

    /// <summary>
    /// Tests connectivity to the server using its stored credentials.
    /// A module an installed plugin serves is tested through the plugin; otherwise cPanel is tested with the
    /// host's own client, and any other module answers <c>PANEL_PLUGIN_UNAVAILABLE</c>, as every provisioning call does.
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

        return ServerModules.Same(server.Module, ServerModules.CPanel)
            ? await TestCPanelAsync(server, ct)
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
        catch (PanelPluginCallException ex)
        {
            // The caller logged the plugin and the exception type; the plugin's own text never reaches the administrator,
            // only the kind of failure it was, as a code the admin can act on.
            return ServerConnectionTestResult.Failed(ex.Failure switch
            {
                PanelPluginFailure.TimedOut => HostpanelErrorCodes.SERVER_CONNECTION_TIMED_OUT,
                PanelPluginFailure.TlsRejected => HostpanelErrorCodes.SERVER_CONNECTION_TLS_FAILED,
                _ => HostpanelErrorCodes.SERVER_CONNECTION_UNREACHABLE,
            });
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
#pragma warning disable CS0618 // Legacy HostingPanel contract (HostingPanelAddress), obsolete since SDK 3.1.0; stays until a cPanel plugin exists.
            // Dialled at the server's API address when it has one, the host name otherwise.
            var connection = new WhmConnection(HostingPanelAddress.BaseUrl(server.ApiEndpointHost, _options.Value.CPanelWhmPort), server.Username, server.ApiToken);
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
}
