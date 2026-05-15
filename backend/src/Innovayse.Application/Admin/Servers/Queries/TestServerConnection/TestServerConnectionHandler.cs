namespace Innovayse.Application.Admin.Servers.Queries.TestServerConnection;
using Innovayse.Application.Admin.Servers.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Domain.Servers.Interfaces;

/// <summary>Handles <see cref="TestServerConnectionQuery"/> — tests connectivity and persists the result.</summary>
public sealed class TestServerConnectionHandler(
    IServerRepository repo,
    IServerConnectionTester tester,
    IUnitOfWork uow)
{
    /// <summary>
    /// Runs the connection test, updates the server's status, and returns the result.
    /// </summary>
    /// <param name="query">The query containing the server ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The test result DTO.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the server is not found.</exception>
    public async Task<TestServerConnectionResultDto> HandleAsync(TestServerConnectionQuery query, CancellationToken ct)
    {
        var server = await repo.FindByIdAsync(query.ServerId, ct)
            ?? throw new InvalidOperationException($"Server {query.ServerId} not found.");

        var (connected, accountsCount, version, errorMessage) = await tester.TestAsync(server, ct);

        server.RecordConnectionTest(connected, accountsCount);
        await uow.SaveChangesAsync(ct);

        return new TestServerConnectionResultDto(connected, accountsCount, version, errorMessage, server.LastTestedAt!.Value);
    }
}
