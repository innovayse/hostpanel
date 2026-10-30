using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Auth.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Clients.Commands.TransferOwnership;

/// <summary>Transfers client account ownership to a different user.</summary>
public sealed class TransferOwnershipHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the person who becomes the owner, wherever people live.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Tells a customer account from a staff account.</summary>
    private readonly ISubjectRoleStore _roles;

    /// <summary>Records the activity log line for this action, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="identity">Reads the person who becomes the owner, wherever people live.</param>
    /// <param name="roles">Tells a customer account from a staff account.</param>
    /// <param name="activity">Records the activity log line for this action, saved with the change it describes.</param>
    public TransferOwnershipHandler(
        IHostpanelDbContext context, IIdentityProvider identity, ISubjectRoleStore roles, IActivityLogger activity)
    {
        _context = context;
        _identity = identity;
        _roles = roles;
        _activity = activity;
    }

    /// <summary>
    /// Transfers account ownership to the specified user. The previous owner stays on the account
    /// as an additional user with every permission (see <see cref="Client.TransferOwnership"/>).
    /// </summary>
    /// <param name="cmd">The transfer ownership command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, <c>CLIENT_NOT_FOUND</c> when the id names no client, <c>IDENTITY_USER_NOT_FOUND</c>
    /// when the new owner is no account or does not hold the client role, or
    /// <c>EXISTING_USER_ALREADY_HAS_CLIENT</c> when the new owner already owns another client:
    /// including one that took the user between the check and the save, which the unique index on
    /// the owner column answers. Transferring to the current owner is refused by
    /// <see cref="Client.TransferOwnership"/> with <c>CLIENT_TRANSFER_SAME_OWNER</c>, and a closed
    /// account with <c>CLIENT_RECORD_CLOSED</c>.
    /// </returns>
    public async Task<Result> HandleAsync(TransferOwnershipCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var client = await _context.Clients
            .Include(c => c.Users)
            .FirstOrDefaultAsync(c => c.Id == cmd.ClientId, ct);
        if (client is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND);
        }

        // A subject that resolves to nobody, or to a staff account, would leave the client owned by
        // a person who cannot sign in to it.
        if (!await _identity.IsClientAccountAsync(_roles, cmd.NewOwnerUserId, ct))
        {
            return Result.FailureCode(HostpanelErrorCodes.IDENTITY_USER_NOT_FOUND);
        }

        // One user owns at most one client (unique index on the owner column): refusing here keeps a
        // transfer onto an existing owner from surfacing as a database error and a 500.
        if (await OwnsAnotherClientAsync(cmd.NewOwnerUserId, cmd.ClientId, ct))
        {
            return Result.FailureCode(HostpanelErrorCodes.EXISTING_USER_ALREADY_HAS_CLIENT);
        }

        client.TransferOwnership(cmd.NewOwnerUserId);

        await _activity.RecordAsync(
            new ActivityLogEntry(client.Id, $"Transferred Ownership - Client ID: {client.Id} - New Owner ID: {cmd.NewOwnerUserId}"), ct);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another writer gave the same user a client between the check above and this save, and
            // the unique index refused. Asked again rather than assumed: any other failed save is not
            // this conflict and must still surface as the error it is.
            _context.DiscardPendingChanges();
            if (await OwnsAnotherClientAsync(cmd.NewOwnerUserId, cmd.ClientId, ct))
            {
                return Result.FailureCode(HostpanelErrorCodes.EXISTING_USER_ALREADY_HAS_CLIENT);
            }

            throw;
        }

        // The new owner may never have registered or ordered here, so the transfer grants the role
        // the portal requires.
        await _roles.AddAsync(cmd.NewOwnerUserId, Role.Client, ct);
        return Result.Success();
    }

    /// <summary>Whether the user is already the owner of a client other than the one being transferred.</summary>
    /// <param name="userId">The Identity user ID of the would-be owner.</param>
    /// <param name="clientId">The client being transferred, which does not count.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the user owns another client.</returns>
    private Task<bool> OwnsAnotherClientAsync(string userId, int clientId, CancellationToken ct) =>
        _context.Clients.AsNoTracking().AnyAsync(c => c.UserId == userId && c.Id != clientId, ct);
}
