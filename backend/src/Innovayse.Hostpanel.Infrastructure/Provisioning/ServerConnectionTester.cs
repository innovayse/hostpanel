using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.Hostpanel.Infrastructure.Integrations.Plesk;
using Innovayse.Hostpanel.Infrastructure.Integrations.Plesk.Models;
using Innovayse.Hostpanel.Infrastructure.Provisioning.Settings;
using Innovayse.Hostpanel.Infrastructure.Resilience;
using Innovayse.SDK.Plugins.HostingPanel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Provisioning;

/// <summary>Tests server connectivity using the appropriate API client based on server module.</summary>
public sealed class ServerConnectionTester : IServerConnectionTester
{
    /// <summary>The CWP API client, for servers on the CWP module.</summary>
    private readonly IHostingPanelApiClient _cwpClient;

    /// <summary>The CWP7 API client, for servers on the CWP7 module.</summary>
    private readonly IHostingPanelV7ApiClient _cwp7Client;

    /// <summary>Builds the named Plesk HTTP client, so the Plesk timeout and resilience profile apply.</summary>
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>Builds the Plesk client's logger.</summary>
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>The port Plesk servers answer on.</summary>
    private readonly IOptions<ProvisioningOptions> _options;

    /// <summary>Records why a connection test failed, detail the caller never sees.</summary>
    private readonly ILogger<ServerConnectionTester> _logger;

    /// <summary>Initializes a new instance of the <see cref="ServerConnectionTester"/> class.</summary>
    /// <param name="cwpClient">The CWP API client, for servers on the CWP module.</param>
    /// <param name="cwp7Client">The CWP7 API client, for servers on the CWP7 module.</param>
    /// <param name="httpClientFactory">Builds the named Plesk HTTP client, so the Plesk timeout and resilience profile apply.</param>
    /// <param name="loggerFactory">Builds the Plesk client's logger.</param>
    /// <param name="options">The port Plesk servers answer on.</param>
    /// <param name="logger">Records why a connection test failed, detail the caller never sees.</param>
    public ServerConnectionTester(
        IHostingPanelApiClient cwpClient,
        IHostingPanelV7ApiClient cwp7Client,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        IOptions<ProvisioningOptions> options,
        ILogger<ServerConnectionTester> logger)
    {
        _cwpClient = cwpClient;
        _cwp7Client = cwp7Client;
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Tests connectivity to the server using its stored credentials.
    /// Supports CWP7, Plesk and legacy CentOS modules; other modules return a not-supported error.
    /// </summary>
    /// <param name="server">The server to test.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The connection result.</returns>
    public async Task<ServerConnectionTestResult> TestAsync(Server server, CancellationToken ct)
    {
        return server.Module switch
        {
            ServerModule.Cwp7 => await TestCwp7Async(server, ct),
            ServerModule.Plesk => await TestPleskAsync(server, ct),
            ServerModule.Centos => await TestLegacyCwpAsync(server, ct),
            _ => ServerConnectionTestResult.Failed(HostpanelErrorCodes.SERVER_CONNECTION_MODULE_UNSUPPORTED),
        };
    }

    /// <summary>
    /// Tests connectivity to a CWP7 server using the IHostingPanelV7ApiClient.
    /// Uses Packages LIST to verify connectivity.
    /// </summary>
    /// <param name="server">The CWP7 server.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The connection result.</returns>
    private async Task<ServerConnectionTestResult> TestCwp7Async(Server server, CancellationToken ct)
    {
        var apiKey = server.AccessHash;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return ServerConnectionTestResult.Failed(HostpanelErrorCodes.SERVER_CONNECTION_NO_ACCESS_HASH);
        }

        try
        {
            var info = await _cwp7Client.GetServerInfoAsync(ConnectionTo(server, apiKey), ct);
            return ServerConnectionTestResult.Succeeded(info.PackagesCount, null);
        }
        catch (Exception ex)
        {
            // The provider's own message never reaches the caller (rules on upstream detail); it
            // is logged here for whoever has to chase the failure up.
            _logger.LogWarning(ex, "CWP7 connection test failed for server {ServerId} ({Hostname})", server.Id, server.Hostname);
            return ServerConnectionTestResult.Failed(HostpanelErrorCodes.SERVER_CONNECTION_UNREACHABLE);
        }
    }

    /// <summary>Tests connectivity to a Plesk server by reading its version and customer count.</summary>
    /// <param name="server">The Plesk server; its access hash holds the Plesk API key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The connection result.</returns>
    private async Task<ServerConnectionTestResult> TestPleskAsync(Server server, CancellationToken ct)
    {
        var apiKey = server.AccessHash;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return ServerConnectionTestResult.Failed(HostpanelErrorCodes.SERVER_CONNECTION_NO_ACCESS_HASH);
        }

        var client = new PleskApiClient(
            _httpClientFactory.CreateClient(HttpClientNames.Plesk),
            _loggerFactory.CreateLogger<PleskApiClient>());
        var connection = new PleskConnection(HostingPanelAddress.BaseUrl(server.Hostname, _options.Value.PleskPort), apiKey);
        try
        {
            var info = await client.GetServerInfoAsync(connection, ct);
            return ServerConnectionTestResult.Succeeded(info.CustomerCount, info.Version);
        }
        catch (Exception ex)
        {
            // Whatever failed (a refusal, a timeout, an address that is not a URL), the panel's own
            // text never reaches the caller; it is logged for whoever has to chase the failure up.
            _logger.LogWarning(ex, "Plesk connection test failed for server {ServerId} ({Hostname})", server.Id, server.Hostname);
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

    /// <summary>Builds the panel connection for a server on the panel's default API port.</summary>
    /// <param name="server">The server to reach.</param>
    /// <param name="apiKey">The credential the server stores for its panel API.</param>
    /// <returns>The connection to hand the panel client.</returns>
    private static HostingPanelConnection ConnectionTo(Server server, string apiKey) =>
        new(server.Hostname, IHostingPanelApiClient.DefaultApiPort, apiKey);
}
