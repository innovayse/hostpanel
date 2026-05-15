namespace Innovayse.Application.Admin.Servers.Interfaces;
using Innovayse.Domain.Servers;

/// <summary>Abstraction for testing connectivity to a provisioning server.</summary>
public interface IServerConnectionTester
{
    /// <summary>
    /// Tests the connection to the given server using its stored credentials.
    /// </summary>
    /// <param name="server">The server to test.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple: (connected, accountsCount, version, errorMessage).</returns>
    Task<(bool Connected, int? AccountsCount, string? Version, string? ErrorMessage)> TestAsync(
        Server server, CancellationToken ct);
}
