using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Domain.Servers;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>Abstraction for testing connectivity to a provisioning server.</summary>
public interface IServerConnectionTester
{
    /// <summary>
    /// Tests the connection to the given server using its stored credentials.
    /// </summary>
    /// <param name="server">The server to test.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether the server answered, what it reported, or why the probe failed.</returns>
    public Task<ServerConnectionTestResult> TestAsync(Server server, CancellationToken ct);
}
