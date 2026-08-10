namespace Innovayse.Application.Tests.Clients;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Clients.Queries.ListClients;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;
using Moq;
using Xunit;

/// <summary>Tests for <see cref="ListClientsHandler"/>.</summary>
public class ListClientsHandlerTests
{
    /// <summary>
    /// IUserService is backed by ASP.NET Identity's UserManager, which shares the
    /// request's scoped DbContext. EF Core throws "A second operation was started on
    /// this context instance" when two of its queries overlap, so the handler must
    /// keep at most one call in flight.
    ///
    /// This guard reproduces that rule: it throws the moment a second call starts
    /// before the previous one returned. Without it the test passes on a thread pool
    /// that happens to serialise the work, and the bug stays invisible.
    /// </summary>
    private sealed class SingleFlightGuard
    {
        private int inFlight;

        public async Task<T> RunAsync<T>(Func<T> result)
        {
            if (Interlocked.Increment(ref inFlight) > 1)
            {
                Interlocked.Decrement(ref inFlight);
                throw new InvalidOperationException(
                    "A second operation was started on this context instance before a previous operation completed.");
            }

            try
            {
                // Yield, so overlapping callers actually interleave rather than each
                // completing synchronously before the next begins.
                await Task.Delay(5);
                return result();
            }
            finally
            {
                Interlocked.Decrement(ref inFlight);
            }
        }
    }

    [Fact]
    public async Task HandleAsync_PageOfManyClients_NeverOverlapsUserServiceCalls()
    {
        var clients = Enumerable.Range(1, 20)
            .Select(i => Client.Create($"user-{i}", "First", $"Last{i}", $"user-{i}@example.com"))
            .ToList();

        var repo = new Mock<IClientRepository>();
        repo.Setup(r => r.ListAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<ClientStatus?>(),
                It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Client>)clients, clients.Count));

        var guard = new SingleFlightGuard();
        var users = new Mock<IUserService>();

        users.Setup(u => u.GetEmailsByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> ids, CancellationToken _) =>
                guard.RunAsync(() => ids.ToDictionary(id => id, id => $"{id}@example.com")));

        users.Setup(u => u.IsTwoFactorEnabledAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => guard.RunAsync(() => id.EndsWith('1')));

        users.Setup(u => u.GetTwoFactorEnabledByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> ids, CancellationToken _) =>
                guard.RunAsync(() => ids.ToDictionary(id => id, id => id.EndsWith('1'))));

        var handler = new ListClientsHandler(repo.Object, users.Object);

        // Before the fix the handler fanned these lookups out with Task.WhenAll. That
        // threw the EF Core concurrency error above, which the API returned to the
        // browser as 400 Bad Request on /api/clients?page=1&pageSize=20.
        var result = await handler.HandleAsync(new ListClientsQuery(1, 20), CancellationToken.None);

        Assert.Equal(20, result.Items.Count());
    }
}