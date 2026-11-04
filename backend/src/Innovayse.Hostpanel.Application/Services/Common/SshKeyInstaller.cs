using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>
/// Records a public key on a service and installs it on the hosting account through the server's key agent: the one
/// path a key reaches an account by, whether the client adds it (<c>AddMySshKey</c>) or the host issues it to a plugin
/// (<c>PluginSshAccessService</c>).
/// </summary>
/// <remarks>
/// The record is saved as <see cref="SshKeySyncState.Pending"/> <b>before</b> the server is called, so a crash or
/// timeout mid-call leaves a record pointing at the right server and key id rather than a key on the server the host
/// knows nothing about. The agent's add is idempotent by key id, so a later retry cannot install it twice. The count
/// against <see cref="SshLimits.MaxKeysPerService"/> and the insert are serialised per service: the same save moves
/// <c>ClientService.SshKeyRevision</c>, so it writes the service row, whose xmin token makes a concurrent add lose with a
/// conflict and count again instead of passing the limit unseen. A lost race of the same key on the unique fingerprint
/// index arrives as the same conflict and, counted again, answers <c>SSH_KEY_ALREADY_INSTALLED</c>. A retry detaches only
/// the key and the service row it staged, never what else the request's context holds (a plugin call's store writes),
/// and waits a short, jittered moment so two racing adds do not collide again in step.
/// </remarks>
public sealed class SshKeyInstaller
{
    /// <summary>How many times an add is counted and saved again after losing the service row to another add.</summary>
    private const int MaxSaveAttempts = 3;

    /// <summary>The longest pause before a retry, in milliseconds; the pause is drawn between zero and this.</summary>
    private const int MaxRetryJitterMilliseconds = 50;

    /// <summary>Data context the key record is saved through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Records a provider failure without putting its detail on the wire.</summary>
    private readonly ILogger<SshKeyInstaller> _logger;

    /// <summary>Initializes the installer.</summary>
    /// <param name="context">Data context the key record is saved through.</param>
    /// <param name="logger">Records a provider failure without putting its detail on the wire.</param>
    public SshKeyInstaller(IHostpanelDbContext context, ILogger<SshKeyInstaller> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Records the key and installs it on the service's account. The caller stages and saves its activity line.</summary>
    /// <param name="context">The service's SSH context; must be <see cref="SshAccessState.Available"/>.</param>
    /// <param name="label">The key's label, already validated.</param>
    /// <param name="key">The parsed public key.</param>
    /// <param name="issuedToPluginId">The plugin the host issues the key to, or <see langword="null"/> for a client's key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The installed key, or <c>SSH_KEY_ALREADY_INSTALLED</c>, <c>SSH_KEY_LIMIT_REACHED</c>, <c>SSH_ACCESS_UNAVAILABLE</c>,
    /// <c>SSH_KEY_INVALID</c> or <c>SSH_SERVER_UNAVAILABLE</c> (the record then says the install failed).
    /// </returns>
    public async Task<Result<SshKey>> InstallAsync(
        SshServiceContext context, string label, SshPublicKey key, string? issuedToPluginId, CancellationToken ct)
    {
        if (context.State is not SshAccessState.Available
            || context.Account is not { } account
            || context.Provider is not { } provider)
        {
            return Result<SshKey>.FailureCode(HostpanelErrorCodes.SSH_ACCESS_UNAVAILABLE);
        }

        SshKey? record = null;
        for (var attempt = 1; record is null; attempt++)
        {
            var staged = await StageAsync(context.Service.Id, account, label, key, issuedToPluginId, ct);
            if (staged.IsFailure)
            {
                return staged;
            }

            try
            {
                await _context.SaveChangesAsync(ct);
                record = staged.Value;
            }
            catch (ConcurrencyConflictException)
            {
                // Another add of this service saved first (the service row's token moved, or the same key won the
                // fingerprint index): drop only what this attempt staged and count again from a fresh read.
                ForgetStaged(context.Service.Id, staged.Value!);
                if (attempt >= MaxSaveAttempts)
                {
                    return Result<SshKey>.FailureCode(HostpanelErrorCodes.SSH_SERVER_UNAVAILABLE);
                }

                // Not a secret, so the shared generator is fine: it only spreads two racing retries apart.
                await Task.Delay(Random.Shared.Next(MaxRetryJitterMilliseconds + 1), ct);
            }
        }

        try
        {
            await provider.AddKeyAsync(account, record.KeyId, record.PublicKey, ct);
            record.MarkInstalled();
        }
        catch (SshProviderException ex)
        {
            _logger.LogWarning(ex, "SSH key {KeyId} install on server {ServerId} failed: {Failure}",
                record.KeyId, account.ServerId, ex.Failure);

            if (!ex.IsOutcomeUnknown)
            {
                // The agent answered with a definite refusal: the key is not on the server, so the record would only
                // describe something that never happened.
                _context.SshKeys.Remove(record);
                await _context.SaveChangesAsync(ct);
                return Result<SshKey>.FailureCode(ex.Failure switch
                {
                    SshProviderFailure.DuplicateKey => HostpanelErrorCodes.SSH_KEY_ALREADY_INSTALLED,
                    SshProviderFailure.TooManyKeys => HostpanelErrorCodes.SSH_KEY_LIMIT_REACHED,
                    SshProviderFailure.SshDisabled or SshProviderFailure.AccountSuspended or SshProviderFailure.AccountNotEligible
                        => HostpanelErrorCodes.SSH_ACCESS_UNAVAILABLE,
                    SshProviderFailure.InvalidKey => HostpanelErrorCodes.SSH_KEY_INVALID,
                    _ => HostpanelErrorCodes.SSH_SERVER_UNAVAILABLE,
                });
            }

            record.MarkInstallFailed(ex.Failure);
            await _context.SaveChangesAsync(ct);
            return Result<SshKey>.FailureCode(HostpanelErrorCodes.SSH_SERVER_UNAVAILABLE);
        }

        return Result<SshKey>.Success(record);
    }

    /// <summary>Stops tracking the key and the service row one attempt staged, and nothing else.</summary>
    /// <param name="serviceId">The service the attempt wrote.</param>
    /// <param name="staged">The key the attempt added.</param>
    private void ForgetStaged(int serviceId, SshKey staged)
    {
        _context.Detach(staged);
        if (_context.ClientServices.Local.FirstOrDefault(s => s.Id == serviceId) is { } service)
        {
            _context.Detach(service);
        }
    }

    /// <summary>
    /// Counts the service's live keys and, when there is room, stages the new key and moves the service's SSH key
    /// revision, so the save that records the key also writes the service row and two adds cannot both pass the limit.
    /// </summary>
    /// <param name="serviceId">The service.</param>
    /// <param name="account">The account the key is for.</param>
    /// <param name="label">The key's label.</param>
    /// <param name="key">The parsed public key.</param>
    /// <param name="issuedToPluginId">The plugin the key is issued to, or <see langword="null"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The staged key, or <c>SSH_KEY_ALREADY_INSTALLED</c>, <c>SSH_KEY_LIMIT_REACHED</c> or <c>SSH_ACCESS_UNAVAILABLE</c>.</returns>
    private async Task<Result<SshKey>> StageAsync(
        int serviceId, SshAccount account, string label, SshPublicKey key, string? issuedToPluginId, CancellationToken ct)
    {
        var service = await _context.ClientServices.FirstOrDefaultAsync(s => s.Id == serviceId, ct);
        if (service is null)
        {
            return Result<SshKey>.FailureCode(HostpanelErrorCodes.SSH_ACCESS_UNAVAILABLE);
        }

        var existing = await _context.SshKeys
            .Where(k => k.ClientServiceId == serviceId && k.SyncState != SshKeySyncState.Revoked)
            .Select(k => k.Fingerprint)
            .ToListAsync(ct);
        if (existing.Contains(key.Fingerprint))
        {
            return Result<SshKey>.FailureCode(HostpanelErrorCodes.SSH_KEY_ALREADY_INSTALLED);
        }

        if (existing.Count >= SshLimits.MaxKeysPerService)
        {
            return Result<SshKey>.FailureCode(HostpanelErrorCodes.SSH_KEY_LIMIT_REACHED);
        }

        var record = SshKey.Create(
            serviceId, account.ServerId, account.Username, label, key.Canonical, key.Fingerprint, issuedToPluginId);
        service.RecordSshKeyAdded();
        _context.SshKeys.Add(record);
        return Result<SshKey>.Success(record);
    }
}
