using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Queries.GetServerSshTarget;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Infrastructure.Provisioning;

/// <summary>
/// Default <see cref="ISshKeyRevoker"/>. Asks the Admin servers module for the key's pinned server
/// through <see cref="GetServerSshTargetQuery"/> and calls the agent there.
/// </summary>
public sealed class SshKeyRevoker : ISshKeyRevoker
{
    /// <summary>Message bus, for the server lookup owned by the servers module.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Provider factory.</summary>
    private readonly ISshKeyProviderFactory _factory;

    /// <summary>The source of the moment a confirmed revocation is recorded at.</summary>
    private readonly IClock _clock;

    /// <summary>Logger.</summary>
    private readonly ILogger<SshKeyRevoker> _logger;

    /// <summary>Initialises the revoker.</summary>
    /// <param name="bus">Message bus, for the server lookup owned by the servers module.</param>
    /// <param name="factory">Provider factory.</param>
    /// <param name="clock">The source of the moment a confirmed revocation is recorded at.</param>
    /// <param name="logger">Logger.</param>
    public SshKeyRevoker(IMessageBus bus, ISshKeyProviderFactory factory, IClock clock, ILogger<SshKeyRevoker> logger)
    {
        _bus = bus;
        _factory = factory;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> RevokeOnPinnedServerAsync(SshKey key, CancellationToken ct)
    {
        var provider = await ProviderForAsync(key, ct);
        if (provider is null)
        {
            key.MarkRevokeFailed(SshProviderFailure.AgentNotConfigured);
            _logger.LogWarning("SSH key {KeyId} could not be revoked: server {ServerId} has no usable agent.", key.KeyId, key.ServerId);
            return false;
        }

        try
        {
            await provider.RevokeKeyAsync(new SshAccount(key.ServerId, key.AccountUsername), key.KeyId, ct);
            key.MarkRevoked(_clock.UtcNow);
            return true;
        }
        catch (SshProviderException ex)
        {
            // AccountNotFound included: the account may be gone, or only gone from the panel's
            // list while its home (and the key) remain. Not confirmed, so not marked revoked.
            key.MarkRevokeFailed(ex.Failure);
            _logger.LogWarning(ex, "SSH key {KeyId} revoke on pinned server {ServerId} failed: {Failure}", key.KeyId, key.ServerId, ex.Failure);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> SuspendOnPinnedServerAsync(SshKey key, CancellationToken ct)
    {
        var provider = await ProviderForAsync(key, ct);
        if (provider is null)
        {
            key.RecordSyncError(SshProviderFailure.AgentNotConfigured);
            _logger.LogWarning("SSH key {KeyId} could not be suspended: server {ServerId} has no usable agent.", key.KeyId, key.ServerId);
            return false;
        }

        try
        {
            await provider.RevokeKeyAsync(new SshAccount(key.ServerId, key.AccountUsername), key.KeyId, ct);
            key.MarkSuspended();
            return true;
        }
        catch (SshProviderException ex)
        {
            // Not confirmed gone, so it may still log in: it stays installed, with the reason.
            key.RecordSyncError(ex.Failure);
            _logger.LogWarning(ex, "SSH key {KeyId} suspension on pinned server {ServerId} failed: {Failure}", key.KeyId, key.ServerId, ex.Failure);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> RestoreOnPinnedServerAsync(SshKey key, CancellationToken ct)
    {
        var provider = await ProviderForAsync(key, ct);
        if (provider is null)
        {
            key.MarkInstallFailed(SshProviderFailure.AgentNotConfigured);
            _logger.LogWarning("SSH key {KeyId} could not be restored: server {ServerId} has no usable agent.", key.KeyId, key.ServerId);
            return false;
        }

        try
        {
            // Under the same sources the key was issued with, so a key limited to some addresses never comes back open.
            await provider.AddKeyAsync(new SshAccount(key.ServerId, key.AccountUsername), key.KeyId, key.PublicKey, key.AllowedFrom, ct);
            key.MarkInstalled();
            return true;
        }
        catch (SshProviderException ex) when (ex.Failure is SshProviderFailure.AccountSuspended)
        {
            // The panel has not unlocked the account yet; the restore runs again once it has.
            key.RecordSyncError(ex.Failure);
            _logger.LogInformation("SSH key {KeyId} not restored yet: account on server {ServerId} is still suspended.", key.KeyId, key.ServerId);
            return false;
        }
        catch (SshProviderException ex)
        {
            key.MarkInstallFailed(ex.Failure);
            _logger.LogWarning(ex, "SSH key {KeyId} restore on pinned server {ServerId} failed: {Failure}", key.KeyId, key.ServerId, ex.Failure);
            return false;
        }
    }

    /// <summary>
    /// The agent of the server the key was issued on. A disabled server is still reachable: switching
    /// the feature off must not strand keys that can log in. Only a server with no usable agent
    /// settings cannot be asked.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The provider, or null when the pinned server has no usable agent.</returns>
    private async Task<ISshKeyProvider?> ProviderForAsync(SshKey key, CancellationToken ct)
    {
        var lookup = await _bus.InvokeAsync<Result<ServerSshTargetDto>>(new GetServerSshTargetQuery(key.ServerId), ct);
        // A successful lookup always carries its value, so Value is only read once IsFailure is false.
        if (lookup.IsFailure || lookup.Value!.Agent is not { IsComplete: true })
        {
            return null;
        }

        return _factory.CreateFor(lookup.Value);
    }
}
