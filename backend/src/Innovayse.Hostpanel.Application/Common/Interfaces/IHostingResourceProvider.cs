using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Ssh;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Websites and databases on one hosting server. Created per server by <see cref="IHostingResourceProviderFactory"/>;
/// every order names the account explicitly and the implementation refuses an account whose
/// <see cref="SshAccount.ServerId"/> is not its own. Every operation is idempotent on the server, so a retry after an
/// unknown outcome is safe.
/// </summary>
public interface IHostingResourceProvider
{
    /// <summary>Reads what the server can set up: which of the operations it has, and the PHP versions it offers.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The server's capabilities.</returns>
    /// <exception cref="SshProviderException">Thrown when the server fails or refuses.</exception>
    public Task<HostingCapabilities> GetCapabilitiesAsync(CancellationToken ct);

    /// <summary>Makes sure the website exists as the order says; asking again for the same order changes nothing.</summary>
    /// <param name="order">The website to set up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the server confirmed the website.</returns>
    /// <exception cref="SshProviderException">Thrown when the server fails or refuses.</exception>
    public Task EnsureWebsiteAsync(WebsiteOrder order, CancellationToken ct);

    /// <summary>Removes a website. Idempotent: removing an absent website succeeds.</summary>
    /// <param name="removal">The website to take down.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the server confirmed the website is gone.</returns>
    /// <exception cref="SshProviderException">Thrown when the server fails or refuses.</exception>
    public Task RemoveWebsiteAsync(WebsiteRemoval removal, CancellationToken ct);

    /// <summary>
    /// Makes sure the database exists for the order's label. The password comes back only when this call created the
    /// database, never on a repeat.
    /// </summary>
    /// <param name="order">The database to set up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The database's names, with the password when it was just created.</returns>
    /// <exception cref="SshProviderException">Thrown when the server fails or refuses.</exception>
    public Task<ProvisionedDatabase> EnsureDatabaseAsync(DatabaseOrder order, CancellationToken ct);

    /// <summary>Drops a database and its user. Idempotent: dropping an absent database succeeds.</summary>
    /// <param name="removal">The database to drop.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the server confirmed the database is gone.</returns>
    /// <exception cref="SshProviderException">Thrown when the server fails or refuses.</exception>
    public Task RemoveDatabaseAsync(DatabaseRemoval removal, CancellationToken ct);
}
