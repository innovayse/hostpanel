using Innovayse.SDK.Plugins.Identity;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Reads the account a client is linked to in another system, for a module that does not own clients, so no
/// module outside Clients reads the clients table itself.
/// </summary>
public interface IClientExternalAccounts
{
    /// <summary>Reads the external account a client is linked to.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The account, or <see langword="null"/> when the client is not linked to one or does not exist.</returns>
    Task<ExternalAccount?> FindAsync(int clientId, CancellationToken ct);
}
