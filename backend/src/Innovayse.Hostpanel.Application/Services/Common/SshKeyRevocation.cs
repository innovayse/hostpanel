using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>
/// Takes a live key off the account it was installed on, and only off that account: through the service's current
/// agent when the key still belongs to the service's current server and account, otherwise on the server and account it
/// was pinned to (<see cref="ISshKeyRevoker"/>), so the service's current server is never asked about a key it never
/// held. The one path a key leaves an account by, whether the client revokes it or the plugin it was issued to does.
/// </summary>
/// <remarks>
/// Revocation is allowed while the account's shell is disabled: removing access must never depend on access being
/// enabled. Records the outcome on the key and never saves; the caller saves with its activity line.
/// </remarks>
public sealed class SshKeyRevocation
{
    /// <summary>Removes a key on the server it was issued on, when the service no longer points there.</summary>
    private readonly ISshKeyRevoker _revoker;

    /// <summary>The source of the revocation moment.</summary>
    private readonly IClock _clock;

    /// <summary>Records a provider failure without putting its detail on the wire.</summary>
    private readonly ILogger<SshKeyRevocation> _logger;

    /// <summary>Initializes the revocation.</summary>
    /// <param name="revoker">Removes a key on the server it was issued on, when the service no longer points there.</param>
    /// <param name="clock">The source of the revocation moment.</param>
    /// <param name="logger">Records a provider failure without putting its detail on the wire.</param>
    public SshKeyRevocation(ISshKeyRevoker revoker, IClock clock, ILogger<SshKeyRevocation> logger)
    {
        _revoker = revoker;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Revokes the key and records the outcome on it. Does not save.</summary>
    /// <param name="context">The service's SSH context.</param>
    /// <param name="key">A tracked, active key of the service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> once the server confirmed the key is gone; <see langword="false"/> when it did not, and
    /// the key records why.
    /// </returns>
    public async Task<bool> RevokeAsync(SshServiceContext context, SshKey key, CancellationToken ct)
    {
        if (context.State is SshAccessState.Available
            && context.Provider is { } provider
            && context.Account is { } account
            && key.IsBoundTo(account.ServerId, account.Username))
        {
            return await RevokeOnCurrentServerAsync(provider, account, key, ct);
        }

        return await _revoker.RevokeOnPinnedServerAsync(key, ct);
    }

    /// <summary>Revokes through the service's current agent.</summary>
    /// <param name="provider">The current server's provider.</param>
    /// <param name="account">The service's current account, which the key is bound to.</param>
    /// <param name="key">The tracked key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether the server confirmed the key is gone; the key records the outcome either way.</returns>
    private async Task<bool> RevokeOnCurrentServerAsync(
        ISshKeyProvider provider, SshAccount account, SshKey key, CancellationToken ct)
    {
        try
        {
            await provider.RevokeKeyAsync(account, key.KeyId, ct);
            key.MarkRevoked(_clock.UtcNow);
            return true;
        }
        catch (SshProviderException ex)
        {
            _logger.LogWarning(ex, "SSH key {KeyId} revoke on server {ServerId} failed: {Failure}",
                key.KeyId, account.ServerId, ex.Failure);
            key.MarkRevokeFailed(ex.Failure);
            return false;
        }
    }
}
