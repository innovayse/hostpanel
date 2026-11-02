using Innovayse.Hostpanel.Application.Auth.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Clients.Commands.ConfirmMyEmailChange;

/// <summary>
/// Moves the caller's sign-in to the new address once the mailed token matches, which also ends
/// every session the account held, and tells the previous address about it.
/// </summary>
public sealed class ConfirmMyEmailChangeHandler
{
    /// <summary>Whose account; the command does not say, and must not.</summary>
    private readonly ICurrentUserService _caller;

    /// <summary>Reads the address the account signs in with before the change, for the notice.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Checks the token and writes the new address, where this deployment owns the accounts.</summary>
    private readonly IUserProvisioning _provisioning;

    /// <summary>The module's data context, for seeding the notice template.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine message bus, for sending the notice.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="caller">Whose account; the command does not say, and must not.</param>
    /// <param name="identity">Reads the address the account signs in with before the change, for the notice.</param>
    /// <param name="provisioning">Checks the token and writes the new address, where this deployment owns the accounts.</param>
    /// <param name="context">The module's data context, for seeding the notice template.</param>
    /// <param name="bus">Wolverine message bus, for sending the notice.</param>
    public ConfirmMyEmailChangeHandler(
        ICurrentUserService caller,
        IIdentityProvider identity,
        IUserProvisioning provisioning,
        IHostpanelDbContext context,
        IMessageBus bus)
    {
        _caller = caller;
        _identity = identity;
        _provisioning = provisioning;
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles <see cref="ConfirmMyEmailChangeCommand"/>.</summary>
    /// <param name="cmd">The new address and the token from the link.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the address has changed (every earlier session, the caller's included, has then
    /// ended); <c>IDENTITY_USER_NOT_FOUND</c> when the account behind the credential is gone;
    /// <c>TOKEN_INVALID_OR_EXPIRED</c> when the token does not match; <c>EMAIL_ALREADY_IN_USE</c>
    /// when another account took the address meanwhile; or
    /// <c>USER_PROVISIONING_NOT_ALLOWED_CHANGE_EMAIL</c> where an external sign-on provider owns the accounts.
    /// </returns>
    public async Task<Result> HandleAsync(ConfirmMyEmailChangeCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var subject = _caller.RequireUserId();
        var account = await _identity.FindBySubjectAsync(subject, ct);
        if (account is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.IDENTITY_USER_NOT_FOUND);
        }

        var changed = await _provisioning.ConfirmEmailChangeAsync(
            new EmailChangeConfirmation(subject, cmd.Email, cmd.Token), ct);
        if (changed.IsFailure)
        {
            return Result.FailureCode(changed.ErrorCode!);
        }

        if (!changed.Value)
        {
            return Result.FailureCode(HostpanelErrorCodes.TOKEN_INVALID_OR_EXPIRED);
        }

        // The previous address hears about it, so a change the holder did not make does not go
        // unnoticed by the one inbox they still control. The address is encoded because the
        // template is HTML and the address is text the caller typed.
        await EmailChangedNoticeTemplateSeeder.EnsureSeededAsync(_context, ct);
        // No idempotency key: this runs inside the caller's request and is never replayed by a queue, and a
        // repeated request is a deliberate new change that must be mailed again.
        await _bus.InvokeAsync(new SendEmailCommand(
            account.Email,
            account.Language,
            EmailChangedNoticeTemplateSeeder.Slug,
            new EmailChangedNoticeTemplateModel(cmd.Email)), ct);

        return Result.Success();
    }
}
