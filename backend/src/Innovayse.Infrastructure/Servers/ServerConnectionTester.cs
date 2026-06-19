namespace Innovayse.Infrastructure.Servers;
using Innovayse.Application.Admin.Servers.Interfaces;
using Innovayse.Domain.Servers;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Logging;

/// <summary>Tests server connectivity using the appropriate API client based on server module.</summary>
public sealed class ServerConnectionTester(
    ICwpApiClient cwpClient,
    ICwp7ApiClient cwp7Client,
    ILogger<ServerConnectionTester> logger)
    : IServerConnectionTester
{
    /// <summary>
    /// Tests connectivity to the server using its stored credentials.
    /// Supports CWP7 and legacy CentOS modules; other modules return a not-supported error.
    /// </summary>
    /// <param name="server">The server to test.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Connection result tuple.</returns>
    public async Task<(bool Connected, int? AccountsCount, string? Version, string? ErrorMessage)> TestAsync(
        Server server, CancellationToken ct)
    {
        return server.Module switch
        {
            ServerModule.Cwp7 => await TestCwp7Async(server, ct),
            ServerModule.Centos => await TestLegacyCwpAsync(server, ct),
            _ => (false, null, null, $"Connection testing is not yet supported for module '{server.Module}'."),
        };
    }

    /// <summary>
    /// Tests connectivity to a CWP7 server using the ICwp7ApiClient.
    /// Uses Packages LIST to verify connectivity.
    /// </summary>
    /// <param name="server">The CWP7 server.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Connection result tuple.</returns>
    private async Task<(bool Connected, int? AccountsCount, string? Version, string? ErrorMessage)> TestCwp7Async(
        Server server, CancellationToken ct)
    {
        var apiKey = server.AccessHash;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, null, null, "No access hash configured for this server.");
        }

        const string port = "2304";

        try
        {
            var packagesCount = await cwp7Client.GetServerInfoAsync(server.Hostname, port, apiKey, ct);
            return (true, packagesCount, null, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CWP7 connection test failed for server {ServerId} ({Hostname})", server.Id, server.Hostname);
            return (false, null, null, ex.Message);
        }
    }

    /// <summary>
    /// Tests connectivity to a legacy CentOS Web Panel server using ICwpApiClient.
    /// </summary>
    /// <param name="server">The legacy CWP server.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Connection result tuple.</returns>
    private async Task<(bool Connected, int? AccountsCount, string? Version, string? ErrorMessage)> TestLegacyCwpAsync(
        Server server, CancellationToken ct)
    {
        var apiKey = server.AccessHash ?? server.ApiToken;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, null, null, "No API key or access hash configured for this server.");
        }

        const string port = "2304";

        try
        {
            var (accountsCount, version) = await cwpClient.GetServerInfoAsync(server.Hostname, port, apiKey, ct);
            return (true, accountsCount, version, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CWP connection test failed for server {ServerId} ({Hostname})", server.Id, server.Hostname);
            return (false, null, null, ex.Message);
        }
    }
}
