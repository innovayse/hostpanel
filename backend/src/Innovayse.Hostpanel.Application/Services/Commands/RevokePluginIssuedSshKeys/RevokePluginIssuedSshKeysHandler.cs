using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Services.Commands.RevokePluginIssuedSshKeys;

/// <summary>
/// Revokes every live key issued to a removed plugin through <see cref="SshKeyRevocation"/>, the path every revoke takes:
/// the service's current agent when the key still belongs to its account, otherwise the server it was pinned to. A key
/// the agent does not confirm stays visible as <see cref="SshKeySyncState.RevokeFailed"/> with the reason, and is tried
/// again on the next delivery, since it is still live.
/// </summary>
public sealed class RevokePluginIssuedSshKeysHandler
{
    /// <summary>The Services module's data context, owner of the keys.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves each key's service, server and account.</summary>
    private readonly ISshServiceContextResolver _resolver;

    /// <summary>Takes a key off the account it was installed on.</summary>
    private readonly SshKeyRevocation _revocation;

    /// <summary>Stages an activity line per revoked key on its client, in the same save.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The Services module's data context, owner of the keys.</param>
    /// <param name="resolver">Resolves each key's service, server and account.</param>
    /// <param name="revocation">Takes a key off the account it was installed on.</param>
    /// <param name="activity">Stages an activity line per revoked key on its client, in the same save.</param>
    public RevokePluginIssuedSshKeysHandler(
        IHostpanelDbContext context, ISshServiceContextResolver resolver, SshKeyRevocation revocation, IActivityLogger activity)
    {
        _context = context;
        _resolver = resolver;
        _revocation = revocation;
        _activity = activity;
    }

    /// <summary>Handles <see cref="RevokePluginIssuedSshKeysCommand"/>.</summary>
    /// <param name="cmd">The removed plugin.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>How many keys were revoked and how many the agents did not confirm; never a refusal.</returns>
    public async Task<Result<PluginSshKeyRevocationDto>> HandleAsync(RevokePluginIssuedSshKeysCommand cmd, CancellationToken ct)
    {
        var keys = await _context.SshKeys
            .Where(k => k.IssuedToPluginId == cmd.PluginId && k.SyncState != SshKeySyncState.Revoked)
            .OrderBy(k => k.Id)
            .ToListAsync(ct);

        var revoked = 0;
        var failed = 0;
        foreach (var key in keys)
        {
            var context = await _resolver.ResolveAsync(key.ClientServiceId, ct);

            // A key lives only as long as its service row (cascade), so a resolved context is the normal case; without
            // one the pinned-server revoke is the only safe target, which SshKeyRevocation chooses for any context that
            // does not own the key, so a missing service is counted as not confirmed and left for the next delivery.
            if (context is null || !await _revocation.RevokeAsync(context, key, ct))
            {
                failed++;
                continue;
            }

            revoked++;
            await _activity.RecordAsync(
                new ActivityLogEntry(
                    context.Service.ClientId,
                    $"Revoked SSH key \"{key.Label}\" ({key.Fingerprint}) issued to removed plugin {cmd.PluginId} "
                        + $"from service #{context.Service.Id}"),
                ct);
        }

        // One save: every key's outcome (revoked or the failure it recorded) and the activity lines together.
        await _context.SaveChangesAsync(ct);
        return Result<PluginSshKeyRevocationDto>.Success(new PluginSshKeyRevocationDto(revoked, failed));
    }
}
