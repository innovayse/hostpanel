namespace Innovayse.Infrastructure.Servers;
using Innovayse.Application.Admin.Servers.Interfaces;
using Innovayse.Domain.Servers;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Logging;

/// <summary>Tests server connectivity using the appropriate API client based on server module.</summary>
public sealed class ServerConnectionTester(ICwpApiClient cwpClient, ILogger<ServerConnectionTester> logger)
    : IServerConnectionTester
{
    /// <summary>
    /// Tests connectivity to the server using its stored credentials.
    /// Currently supports CWP (Cwp7) modules; other modules return a not-supported error.
    /// </summary>
    /// <param name="server">The server to test.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Connection result tuple.</returns>
    public async Task<(bool Connected, int? AccountsCount, string? Version, string? ErrorMessage)> TestAsync(
        Server server, CancellationToken ct)
    {
        if (server.Module != ServerModule.Cwp7)
        {
            return (false, null, null, $"Connection testing is not yet supported for module '{server.Module}'.");
        }

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
