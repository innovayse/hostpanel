using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Clients.Commands.InviteUserToClient;

/// <summary>
/// Validates the invitation preconditions, creates an <see cref="Invitation"/> entity, seeds the
/// email template on first use, and sends the invitation email.
/// </summary>
public sealed class InviteUserToClientHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the owner and any existing account with the invited address.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Wolverine message bus, for sending the invitation email.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Where the client portal lives, for the link in the invitation mail.</summary>
    private readonly IOptions<ClientPortalOptions> _clientPortal;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Records the activity log line for this action, saved with the invitation it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="identity">Reads the owner and any existing account with the invited address.</param>
    /// <param name="bus">Wolverine message bus, for sending the invitation email.</param>
    /// <param name="clientPortal">Where the client portal lives, for the link in the invitation mail.</param>
    /// <param name="clock">The source of the current moment.</param>
    /// <param name="activity">Records the activity log line for this action, saved with the invitation it describes.</param>
    public InviteUserToClientHandler(
        IHostpanelDbContext context,
        IIdentityProvider identity,
        IMessageBus bus,
        IOptions<ClientPortalOptions> clientPortal,
        IClock clock,
        IActivityLogger activity)
    {
        _context = context;
        _identity = identity;
        _bus = bus;
        _clientPortal = clientPortal;
        _clock = clock;
        _activity = activity;
    }

    /// <summary>Invites a user to a client account by creating an invitation and sending an email.</summary>
    /// <param name="cmd">The invite command with client id, email, name and permissions.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, with an activity log line for the client, or <c>CLIENT_NOT_FOUND</c>, <c>ACCOUNT_OWNER_NOT_FOUND</c>,
    /// <c>CANNOT_INVITE_ACCOUNT_OWNER</c>, <c>USER_ALREADY_LINKED</c> or
    /// <c>INVITATION_ALREADY_PENDING</c>.
    /// </returns>
    public async Task<Result> HandleAsync(InviteUserToClientCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var client = await _context.Clients
            .Include(c => c.Users)
            .FirstOrDefaultAsync(c => c.Id == cmd.ClientId, ct);
        if (client is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND);
        }

        // The message deliberately omits client.UserId. This runs on a client-portal endpoint too,
        // and that id is the *owner's* internal identity key: something the person inviting never
        // supplied and has no use for.
        var owner = await _identity.FindBySubjectAsync(client.UserId, ct);
        if (owner is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.ACCOUNT_OWNER_NOT_FOUND);
        }

        if (string.Equals(owner.Email, cmd.Email, StringComparison.OrdinalIgnoreCase))
        {
            return Result.FailureCode(HostpanelErrorCodes.CANNOT_INVITE_ACCOUNT_OWNER);
        }

        // An exact lookup: inviting "sam@example.com" to a client that already has
        // "samuel@example.com" linked must not be refused for a name that only shares a prefix.
        var existing = await _identity.FindByEmailAsync(cmd.Email, ct);
        if (existing is not null && client.Users.Any(u => u.UserId == existing.Subject))
        {
            return Result.FailureCode(HostpanelErrorCodes.USER_ALREADY_LINKED);
        }

        var now = _clock.UtcNow;
        var pending = await _context.Invitations.FirstOrDefaultAsync(
            i => i.Email == cmd.Email && i.ClientId == cmd.ClientId && i.AcceptedAt == null && i.ExpiresAt > now,
            ct);
        if (pending is not null)
        {
            return Result.FailureCode(HostpanelErrorCodes.INVITATION_ALREADY_PENDING);
        }

        var invitation = Invitation.Create(cmd.ClientId, cmd.Email, cmd.FirstName, cmd.LastName, (ClientPermission)cmd.Permissions, now);
        _context.Invitations.Add(invitation);

        // The line names the client only, never the invited address: the log is read by staff and
        // carries no personal data. The portal's invite reaches this handler too, so both are audited here.
        await _activity.RecordAsync(new ActivityLogEntry(client.Id, $"Invited User - Client ID: {client.Id}"), ct);
        await _context.SaveChangesAsync(ct);

        await UserInviteTemplateSeeder.EnsureSeededAsync(_context, ct);

        var clientBaseUrl = _clientPortal.Value.BaseUrl;
        var inviteLink = $"{clientBaseUrl}/client/accept-invite?token={Uri.EscapeDataString(invitation.Token)}";

        // A person who already has an account reads their own language; a newcomer has chosen none
        // yet, so they get the language of the client they are invited to.
        // No idempotency key: this runs inside the caller's request and is never replayed by a queue, and a
        // repeated request is a deliberate new invitation that must be mailed again.
        await _bus.InvokeAsync(new SendEmailCommand(
            cmd.Email,
            existing?.Language ?? owner.Language,
            UserInviteTemplateSeeder.Slug,
            new UserInviteTemplateModel(inviteLink, $"{client.FirstName} {client.LastName}", cmd.FirstName)), ct);

        return Result.Success();
    }
}
