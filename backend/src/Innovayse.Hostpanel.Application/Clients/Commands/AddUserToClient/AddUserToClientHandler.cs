using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Auth.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Clients.Commands.AddUserToClient;

/// <summary>Verifies the user is a customer account, then adds them to the client with the specified permissions.</summary>
public sealed class AddUserToClientHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the person being linked, wherever people live.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Tells a customer account from a staff account.</summary>
    private readonly ISubjectRoleStore _roles;

    /// <summary>Records the activity log line for this action, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="identity">Reads the person being linked, wherever people live.</param>
    /// <param name="roles">Tells a customer account from a staff account.</param>
    /// <param name="activity">Records the activity log line for this action, saved with the change it describes.</param>
    public AddUserToClientHandler(
        IHostpanelDbContext context, IIdentityProvider identity, ISubjectRoleStore roles, IActivityLogger activity)
    {
        _context = context;
        _identity = identity;
        _roles = roles;
        _activity = activity;
    }

    /// <summary>Adds a non-owner user to the client account.</summary>
    /// <param name="cmd">The add user command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>CLIENT_NOT_FOUND</c> when the id names no client, or
    /// <c>IDENTITY_USER_NOT_FOUND</c> when the user id names no identity account or an account that
    /// does not hold the client role: a staff account is never linked to a client. Adding the
    /// account owner or a user already linked is refused by <see cref="Client.AddUser"/> with
    /// <c>CLIENT_USER_IS_OWNER</c> or <c>CLIENT_USER_LINK_DUPLICATE</c>, and a closed account with
    /// <c>CLIENT_RECORD_CLOSED</c>.
    /// </returns>
    public async Task<Result> HandleAsync(AddUserToClientCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var client = await _context.Clients
            .Include(c => c.Users)
            .FirstOrDefaultAsync(c => c.Id == cmd.ClientId, ct);
        if (client is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND);
        }

        if (!await _identity.IsClientAccountAsync(_roles, cmd.MemberUserId, ct))
        {
            return Result.FailureCode(HostpanelErrorCodes.IDENTITY_USER_NOT_FOUND);
        }

        client.AddUser(cmd.MemberUserId, cmd.Permissions);

        await _activity.RecordAsync(
            new ActivityLogEntry(client.Id, $"Added User - Client ID: {client.Id} - User ID: {cmd.MemberUserId}"), ct);
        await _context.SaveChangesAsync(ct);

        // The portal serves only holders of the Client role. A user linked here by staff never
        // registered or ordered, the two other places that grant it, so the link grants it.
        await _roles.AddAsync(cmd.MemberUserId, Role.Client, ct);
        return Result.Success();
    }
}
