using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Services.Commands.RestoreServiceSshKeys;

/// <summary>
/// Puts an unsuspended service's suspended SSH keys back on their pinned server.
/// </summary>
/// <remarks>
/// An agent that answers the account is still suspended leaves the key
/// <see cref="SshKeySyncState.Suspended"/> for the second pass, sent once the panel unlocked the
/// account; any other failure makes it <see cref="SshKeySyncState.InstallFailed"/> with the reason
/// and an activity log line, and the customer can remove it and add it again.
/// </remarks>
public sealed class RestoreServiceSshKeysHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Changes keys on the server they were issued on.</summary>
    private readonly ISshKeyRevoker _revoker;

    /// <summary>Stages the activity log lines in the same save as the keys.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="revoker">Changes keys on the server they were issued on.</param>
    /// <param name="activity">Stages the activity log lines in the same save as the keys.</param>
    public RestoreServiceSshKeysHandler(IHostpanelDbContext context, ISshKeyRevoker revoker, IActivityLogger activity)
    {
        _context = context;
        _revoker = revoker;
        _activity = activity;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The unsuspended service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once every suspended key was attempted, also when there was none;
    /// <c>SERVICE_NOT_FOUND</c> when the id names no service; <c>SERVICE_SSH_KEYS_RESTORE_NOT_ACTIVE</c> when the
    /// service is not active.
    /// </returns>
    public async Task<Result> HandleAsync(RestoreServiceSshKeysCommand cmd, CancellationToken ct)
    {
        // Read-only: only the status and the owner are needed; the keys are what change.
        var service = await _context.ClientServices
            .AsNoTracking()
            .Where(s => s.Id == cmd.ServiceId)
            .Select(s => new { s.ClientId, s.Status })
            .FirstOrDefaultAsync(ct);
        if (service is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        if (service.Status is not ServiceStatus.Active)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_SSH_KEYS_RESTORE_NOT_ACTIVE);
        }

        var keys = await _context.SshKeys
            .Where(k => k.ClientServiceId == cmd.ServiceId && k.SyncState == SshKeySyncState.Suspended)
            .ToListAsync(ct);
        if (keys.Count == 0)
        {
            return Result.Success();
        }

        foreach (var key in keys)
        {
            if (!await _revoker.RestoreOnPinnedServerAsync(key, ct) && key.SyncState is SshKeySyncState.InstallFailed)
            {
                await _activity.RecordAsync(
                    new ActivityLogEntry(
                        service.ClientId,
                        $"Could not restore SSH key \"{key.Label}\" ({key.Fingerprint}) of unsuspended service #{cmd.ServiceId}: {key.LastSyncError}"),
                    ct);
            }
        }

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
