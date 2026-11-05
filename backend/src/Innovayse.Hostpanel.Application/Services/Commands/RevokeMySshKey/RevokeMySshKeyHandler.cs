using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Services.Commands.RevokeMySshKey;

/// <summary>
/// Removes one key of the caller's service from the server it was installed on, through <see cref="SshKeyRevocation"/>.
/// The client may revoke any key of its service, a key the host issued to a plugin included.
/// </summary>
public sealed class RevokeMySshKeyHandler
{
    /// <summary>The rule that says a client may only touch their own services.</summary>
    private readonly IServiceOwnership _ownership;

    /// <summary>Resolves server, provider and account once ownership is settled.</summary>
    private readonly ISshServiceContextResolver _resolver;

    /// <summary>Takes the key off the account it was installed on.</summary>
    private readonly SshKeyRevocation _revocation;

    /// <summary>Data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Stages the activity log line in the same save as the revocation.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Initialises the handler.</summary>
    /// <param name="ownership">The rule that says a client may only touch their own services.</param>
    /// <param name="resolver">Resolves server, provider and account once ownership is settled.</param>
    /// <param name="revocation">Takes the key off the account it was installed on.</param>
    /// <param name="context">Data context.</param>
    /// <param name="activity">Stages the activity log line in the same save as the revocation.</param>
    public RevokeMySshKeyHandler(
        IServiceOwnership ownership, ISshServiceContextResolver resolver, SshKeyRevocation revocation,
        IHostpanelDbContext context, IActivityLogger activity)
    {
        _ownership = ownership;
        _resolver = resolver;
        _revocation = revocation;
        _context = context;
        _activity = activity;
    }

    /// <summary>Handles <see cref="RevokeMySshKeyCommand"/>.</summary>
    /// <param name="cmd">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the server confirmed the removal, or one of <c>SERVICE_NOT_FOUND</c>,
    /// <c>SSH_KEY_NOT_FOUND</c>, <c>SSH_ACCESS_UNAVAILABLE</c> (service not active),
    /// <c>SSH_SERVER_UNAVAILABLE</c>.
    /// </returns>
    public async Task<Result> HandleAsync(RevokeMySshKeyCommand cmd, CancellationToken ct)
    {
        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(cmd.ServiceId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return ownershipOutcome;
        }

        var context = await _resolver.ResolveAsync(cmd.ServiceId, ct);
        if (context is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        var key = await _context.SshKeys.FirstOrDefaultAsync(
            k => k.ClientServiceId == context.Service.Id && k.KeyId == cmd.KeyId, ct);
        if (key is null || !key.IsActive)
        {
            return Result.FailureCode(HostpanelErrorCodes.SSH_KEY_NOT_FOUND);
        }

        if (context.State is SshAccessState.ServiceInactive)
        {
            return Result.FailureCode(HostpanelErrorCodes.SSH_ACCESS_UNAVAILABLE);
        }

        if (!await _revocation.RevokeAsync(context, key, ct))
        {
            // The key records why; it stays visible as possibly still able to log in.
            await _context.SaveChangesAsync(ct);
            return Result.FailureCode(HostpanelErrorCodes.SSH_SERVER_UNAVAILABLE);
        }

        await _activity.RecordAsync(
            new ActivityLogEntry(
                context.Service.ClientId,
                $"Revoked SSH key \"{key.Label}\" ({key.Fingerprint}) from service #{context.Service.Id}"),
            ct);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
