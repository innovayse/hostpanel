using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Ssh;

/// <summary>
/// One ED25519 public key a client asked HostPanel to install on one of their hosting services.
/// </summary>
/// <remarks>
/// <para>
/// The key is <b>pinned</b> to the server and Linux account it was issued for
/// (<see cref="ServerId"/>, <see cref="AccountUsername"/>), copied from the service at creation
/// and never updated. If an administrator later moves the service to another server or renames
/// the account, <see cref="IsBoundTo"/> answers false and no operation is sent anywhere for this
/// key: sending a revoke to the service's <i>new</i> server would act on a different machine,
/// where the same username can belong to someone else entirely.
/// </para>
/// <para>
/// <see cref="KeyId"/> is the stable public identifier. It is written into the server-side
/// comment (<c>hostpanel:&lt;KeyId&gt;</c>) so the agent can find exactly this line again, and it
/// is what the API routes use: the sequential database id is never exposed.
/// </para>
/// </remarks>
public sealed class SshKey : BaseEntity, IAggregateRoot
{
    /// <summary>Gets the stable public identifier of this key.</summary>
    public Guid KeyId { get; private set; }

    /// <summary>Gets the client service this key was issued for.</summary>
    public int ClientServiceId { get; private set; }

    /// <summary>Gets the server the key was issued on. Pinned at creation.</summary>
    public int ServerId { get; private set; }

    /// <summary>Gets the Linux account on <see cref="ServerId"/> the key belongs to. Pinned at creation.</summary>
    public string AccountUsername { get; private set; } = string.Empty;

    /// <summary>Gets the friendly label the client chose.</summary>
    public string Label { get; private set; } = string.Empty;

    /// <summary>Gets the canonical public key line (<c>ssh-ed25519 &lt;base64&gt;</c>), without any comment.</summary>
    public string PublicKey { get; private set; } = string.Empty;

    /// <summary>Gets the OpenSSH SHA256 fingerprint, <c>SHA256:</c> prefixed.</summary>
    public string Fingerprint { get; private set; } = string.Empty;

    /// <summary>Gets the synchronisation state with the server.</summary>
    public SshKeySyncState SyncState { get; private set; }

    /// <summary>Gets why the last failed provider operation failed, if any.</summary>
    public SshProviderFailure? LastSyncError { get; private set; }

    /// <summary>Gets when the key was recorded.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets when the provider confirmed the revocation.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>
    /// Gets the id of the plugin the host issued this key to, or <see langword="null"/> for a key the client added.
    /// Set once, by <see cref="Create"/>: only that plugin may revoke the key through the plugin host API, while the client
    /// may revoke any key of its service.
    /// </summary>
    public string? IssuedToPluginId { get; private set; }

    /// <summary>Gets whether this key is still live from the customer's point of view.</summary>
    public bool IsActive => SyncState is not SshKeySyncState.Revoked;

    /// <summary>EF Core constructor.</summary>
    private SshKey() { }

    /// <summary>
    /// Records a new key in <see cref="SshKeySyncState.Pending"/>, before the provider is called.
    /// </summary>
    /// <param name="clientServiceId">The owning service.</param>
    /// <param name="serverId">The server the service is assigned to right now.</param>
    /// <param name="accountUsername">The Linux account on that server right now.</param>
    /// <param name="label">Friendly label; trimmed.</param>
    /// <param name="publicKey">Canonical <c>ssh-ed25519 &lt;base64&gt;</c> line.</param>
    /// <param name="fingerprint">Its SHA256 fingerprint.</param>
    /// <param name="issuedToPluginId">
    /// The plugin the host issues the key to, or <see langword="null"/> for a key the client adds. Required rather than
    /// defaulted, so no caller records a plugin's key as the client's by leaving it out.
    /// </param>
    /// <returns>The new key.</returns>
    /// <exception cref="ArgumentException">
    /// A text is blank, or <paramref name="issuedToPluginId"/> is set and blank.
    /// </exception>
    public static SshKey Create(
        int clientServiceId, int serverId, string accountUsername,
        string label, string publicKey, string fingerprint, string? issuedToPluginId)
    {
        EnsureText(accountUsername, nameof(accountUsername));
        EnsureText(label, nameof(label));
        EnsureText(publicKey, nameof(publicKey));
        EnsureText(fingerprint, nameof(fingerprint));
        // Only blank is refused here: the plugin id rule (shape, length, reserved ids) belongs to the plugin packaging package,
        // which the domain does not reference, and the id reaching this point is a loaded plugin's, already checked at load.
        if (issuedToPluginId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(issuedToPluginId);
        }

        return new SshKey
        {
            KeyId = Guid.NewGuid(),
            ClientServiceId = clientServiceId,
            ServerId = serverId,
            AccountUsername = accountUsername,
            Label = label.Trim(),
            PublicKey = publicKey,
            Fingerprint = fingerprint,
            IssuedToPluginId = issuedToPluginId,
            SyncState = SshKeySyncState.Pending,
        };
    }

    /// <summary>
    /// Whether this key was issued for exactly this server and account. Every provider call for
    /// the key must be preceded by this check.
    /// </summary>
    /// <param name="serverId">The service's current server.</param>
    /// <param name="accountUsername">The service's current account.</param>
    /// <returns><see langword="true"/> when both match the pinned values.</returns>
    public bool IsBoundTo(int? serverId, string? accountUsername) =>
        serverId == ServerId && string.Equals(accountUsername, AccountUsername, StringComparison.Ordinal);

    /// <summary>Refuses a blank required text, the last line of defence behind the use cases' own checks.</summary>
    /// <param name="value">The value.</param>
    /// <param name="name">The parameter's name, for the exception.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty or white space.</exception>
    private static void EnsureText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", name);
        }
    }

    /// <summary>Whether the host issued this key to <paramref name="pluginId"/>. A client's own key is issued to no plugin.</summary>
    /// <param name="pluginId">The plugin asking.</param>
    /// <returns><see langword="true"/> when the key was issued to exactly that plugin (ordinal comparison).</returns>
    public bool IsIssuedTo(string pluginId)
    {
        return IssuedToPluginId is not null && string.Equals(IssuedToPluginId, pluginId, StringComparison.Ordinal);
    }

    /// <summary>Records that the provider confirmed the install.</summary>
    public void MarkInstalled()
    {
        EnsureNotRevoked();
        SyncState = SshKeySyncState.Installed;
        LastSyncError = null;
    }

    /// <summary>Records that the install failed; the key may or may not be on the server.</summary>
    /// <param name="error">Why the provider call failed.</param>
    public void MarkInstallFailed(SshProviderFailure error)
    {
        EnsureNotRevoked();
        SyncState = SshKeySyncState.InstallFailed;
        LastSyncError = error;
    }

    /// <summary>Records that the provider confirmed the key is gone from the server.</summary>
    /// <param name="revokedAt">When the provider confirmed the removal, UTC.</param>
    public void MarkRevoked(DateTimeOffset revokedAt)
    {
        EnsureNotRevoked();
        SyncState = SshKeySyncState.Revoked;
        RevokedAt = revokedAt;
        LastSyncError = null;
    }

    /// <summary>Records that the revoke failed; the key may still authenticate.</summary>
    /// <param name="error">Why the provider call failed.</param>
    public void MarkRevokeFailed(SshProviderFailure error)
    {
        EnsureNotRevoked();
        SyncState = SshKeySyncState.RevokeFailed;
        LastSyncError = error;
    }

    /// <summary>Records that the provider confirmed the key was taken off the server for a suspension.</summary>
    public void MarkSuspended()
    {
        EnsureNotRevoked();
        SyncState = SshKeySyncState.Suspended;
        LastSyncError = null;
    }

    /// <summary>Records a failed call that left the key's state as it was.</summary>
    /// <param name="error">Why the provider call failed.</param>
    public void RecordSyncError(SshProviderFailure error)
    {
        EnsureNotRevoked();
        LastSyncError = error;
    }

    /// <summary>Refuses any transition out of the terminal state.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the key is already revoked.</exception>
    private void EnsureNotRevoked()
    {
        if (SyncState is SshKeySyncState.Revoked)
        {
            throw new InvalidOperationException("SSH key is already revoked.");
        }
    }
}
