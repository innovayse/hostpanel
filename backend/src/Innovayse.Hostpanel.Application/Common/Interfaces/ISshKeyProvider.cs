using Innovayse.Hostpanel.Domain.Ssh;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// SSH key management capability of one hosting server. Created per server by
/// <see cref="ISshKeyProviderFactory"/>; every call names the account explicitly and the
/// implementation refuses an account whose <see cref="SshAccount.ServerId"/> is not its own.
/// </summary>
public interface ISshKeyProvider
{
    /// <summary>Checks the server's key agent answers, for the admin health check.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The agent's version, provider profile and binding.</returns>
    /// <exception cref="SshProviderException">Thrown when the server fails or refuses.</exception>
    public Task<SshAgentInfo> PingAsync(CancellationToken ct);

    /// <summary>Reports whether SSH access is enabled for the account and how many keys it holds.</summary>
    /// <param name="account">The hosting account on this provider's server.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The status.</returns>
    /// <exception cref="SshProviderException">Thrown when the server fails or refuses.</exception>
    public Task<SshAccessStatus> GetAccessStatusAsync(SshAccount account, CancellationToken ct);

    /// <summary>Lists the HostPanel-managed keys on the account.</summary>
    /// <param name="account">The hosting account on this provider's server.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The keys the server holds.</returns>
    /// <exception cref="SshProviderException">Thrown when the server fails or refuses.</exception>
    public Task<IReadOnlyList<InstalledSshKey>> ListKeysAsync(SshAccount account, CancellationToken ct);

    /// <summary>Installs a key. Idempotent for the same <paramref name="keyId"/> and key.</summary>
    /// <param name="account">The hosting account on this provider's server.</param>
    /// <param name="keyId">Stable key id, written into the key comment.</param>
    /// <param name="publicKey">Canonical <c>ssh-ed25519 &lt;base64&gt;</c> line.</param>
    /// <param name="allowedFrom">
    /// The addresses and CIDR networks the key may be used from (<see cref="SshAllowedFromRules"/>), or an empty list for a
    /// key that works from anywhere. An agent older than 1.6.0 refuses a non-empty list.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The receipt once the server confirmed the install, with the server's host key when it reports one.</returns>
    /// <exception cref="SshProviderException">Thrown when the server fails or refuses.</exception>
    public Task<SshKeyReceipt> AddKeyAsync(
        SshAccount account, Guid keyId, string publicKey, IReadOnlyList<string> allowedFrom, CancellationToken ct);

    /// <summary>Removes a key. Idempotent: removing an absent key succeeds.</summary>
    /// <param name="account">The hosting account on this provider's server.</param>
    /// <param name="keyId">Stable key id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the server confirmed the key is gone.</returns>
    /// <exception cref="SshProviderException">Thrown when the server fails or refuses.</exception>
    public Task RevokeKeyAsync(SshAccount account, Guid keyId, CancellationToken ct);

    /// <summary>
    /// Removes the SSH jail (and its home mount) of an account the panel has already deleted (agent 1.4.0+, op
    /// <c>remove-jail</c>). The server refuses while the account still exists, so a live account's jail is never touched.
    /// </summary>
    /// <param name="account">The deleted hosting account on this provider's server.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when a jail was removed, <see langword="false"/> when there was none.</returns>
    /// <exception cref="SshProviderException">Thrown when the server fails or refuses.</exception>
    public Task<bool> RemoveDeletedAccountJailAsync(SshAccount account, CancellationToken ct);
}
