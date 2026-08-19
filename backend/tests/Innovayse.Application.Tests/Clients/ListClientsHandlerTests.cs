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
    /// The local identity provider runs on ASP.NET Identity's UserManager, which shares the
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
    public async Task HandleAsync_PageOfManyClients_NeverOverlapsIdentityCalls()
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
        var identity = new Mock<IIdentityProvider>();

        identity.Setup(i => i.GetAccountsBySubjectsAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> subjects, CancellationToken _) =>
                guard.RunAsync(() => (IReadOnlyDictionary<string, IdentityAccount>)subjects.ToDictionary(
                    s => s,
                    s => new IdentityAccount(s, $"{s}@example.com", "First", "Last", s.EndsWith('1')))));

        var handler = new ListClientsHandler(repo.Object, identity.Object);

        // Before the fix the handler fanned these lookups out with Task.WhenAll. That
        // threw the EF Core concurrency error above, which the API returned to the
        // browser as 400 Bad Request on /api/clients?page=1&pageSize=20.
        var result = await handler.HandleAsync(new ListClientsQuery(1, 20), CancellationToken.None);

        Assert.Equal(20, result.Items.Count());
    }

    /// <summary>
    /// The page is resolved with one lookup, not one per row. Worth pinning: where the
    /// people live in another service, a per-row lookup is a network round trip per row,
    /// and nothing else in the handler would make that visible.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ResolvesThePageInASingleLookup()
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

        var calls = 0;
        var identity = new Mock<IIdentityProvider>();
        identity.Setup(i => i.GetAccountsBySubjectsAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> subjects, CancellationToken _) =>
            {
                Interlocked.Increment(ref calls);
                return Task.FromResult((IReadOnlyDictionary<string, IdentityAccount>)subjects.ToDictionary(
                    s => s, s => new IdentityAccount(s, $"{s}@example.com", "First", "Last")));
            });

        var handler = new ListClientsHandler(repo.Object, identity.Object);
        await handler.HandleAsync(new ListClientsQuery(1, 20), CancellationToken.None);

        Assert.Equal(1, calls);
    }

    /// <summary>
    /// A client row whose subject resolves to nobody still appears, flagged. These are the
    /// legacy rows every deployment accumulates, and dropping them from the list would
    /// hide accounts that still hold invoices and services.
    /// </summary>
    [Fact]
    public async Task HandleAsync_KeepsRowsWhoseSubjectResolvesToNobody()
    {
        var clients = new List<Client> { Client.Create("gone", "First", "Last", "gone@example.com") };

        var repo = new Mock<IClientRepository>();
        repo.Setup(r => r.ListAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<ClientStatus?>(),
                It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Client>)clients, 1));

        var identity = new Mock<IIdentityProvider>();
        identity.Setup(i => i.GetAccountsBySubjectsAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<string, IdentityAccount>)new Dictionary<string, IdentityAccount>());

        var handler = new ListClientsHandler(repo.Object, identity.Object);
        var result = await handler.HandleAsync(new ListClientsQuery(1, 20), CancellationToken.None);

        var row = Assert.Single(result.Items);
        Assert.True(row.IsUserDeleted);
        Assert.Equal(string.Empty, row.Email);
    }
}