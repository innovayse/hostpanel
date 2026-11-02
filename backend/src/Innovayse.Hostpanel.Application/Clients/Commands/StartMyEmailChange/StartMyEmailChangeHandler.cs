using Innovayse.Hostpanel.Application.Auth.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Clients.Commands.StartMyEmailChange;

/// <summary>
/// Checks the caller's password, issues a token bound to the new address and mails the link that
/// makes the change effective to that address.
/// </summary>
public sealed class StartMyEmailChangeHandler
{
    /// <summary>Whose account; the command does not say, and must not.</summary>
    private readonly ICurrentUserService _caller;

    /// <summary>Checks the password and issues the token, where this deployment owns the accounts.</summary>
    private readonly IUserProvisioning _provisioning;

    /// <summary>Refuses credential changes on the published demo accounts.</summary>
    private readonly IDemoAccountGuard _demoAccounts;

    /// <summary>Reads the caller's account, for the language the mail is written in.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>The module's data context, for seeding the template.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine message bus, for sending the mail.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Where the client portal lives, for the link.</summary>
    private readonly IOptions<ClientPortalOptions> _clientPortal;

    /// <summary>Initializes the handler.</summary>
    /// <param name="caller">Whose account; the command does not say, and must not.</param>
    /// <param name="provisioning">Checks the password and issues the token, where this deployment owns the accounts.</param>
    /// <param name="demoAccounts">Refuses credential changes on the published demo accounts.</param>
    /// <param name="identity">Reads the caller's account, for the language the mail is written in.</param>
    /// <param name="context">The module's data context, for seeding the template.</param>
    /// <param name="bus">Wolverine message bus, for sending the mail.</param>
    /// <param name="clientPortal">Where the client portal lives, for the link.</param>
    public StartMyEmailChangeHandler(
        ICurrentUserService caller,
        IUserProvisioning provisioning,
        IDemoAccountGuard demoAccounts,
        IIdentityProvider identity,
        IHostpanelDbContext context,
        IMessageBus bus,
        IOptions<ClientPortalOptions> clientPortal)
    {
        _caller = caller;
        _provisioning = provisioning;
        _demoAccounts = demoAccounts;
        _identity = identity;
        _context = context;
        _bus = bus;
        _clientPortal = clientPortal;
    }

    /// <summary>Handles <see cref="StartMyEmailChangeCommand"/>.</summary>
    /// <param name="cmd">The new address and the current password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the link is on its way to the new address; <c>DEMO_ACTION_DISABLED</c> for a
    /// published demo account; <c>CURRENT_PASSWORD_INVALID</c> when the password is wrong;
    /// <c>EMAIL_ALREADY_IN_USE</c> when the address already belongs to an account;
    /// <c>IDENTITY_USER_NOT_FOUND</c> when the account behind the credential is gone; or
    /// <c>USER_PROVISIONING_NOT_ALLOWED_CHANGE_EMAIL</c> where an external sign-on provider owns the accounts.
    /// </returns>
    public async Task<Result> HandleAsync(StartMyEmailChangeCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var subject = _caller.RequireUserId();

        var allowed = await _demoAccounts.EnsureCredentialsUnchangedAsync(subject, ct);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        // The token first: it is the half that checks the password and refuses where the accounts
        // are not this deployment's, and nothing is mailed before that is settled.
        var token = await _provisioning.IssueEmailChangeTokenAsync(
            new EmailChangeStart(subject, cmd.NewEmail, cmd.CurrentPassword), ct);
        if (token.IsFailure)
        {
            return Result.FailureCode(token.ErrorCode!);
        }

        await EmailChangeTemplateSeeder.EnsureSeededAsync(_context, ct);

        // To the new address only: following the link is what proves the person holds it.
        var confirmationLink = $"{_clientPortal.Value.BaseUrl}/client/confirm-email-change"
            + $"?token={Uri.EscapeDataString(token.Value!)}&email={Uri.EscapeDataString(cmd.NewEmail)}";

        var account = await _identity.FindBySubjectAsync(subject, ct);
        // No idempotency key: this runs inside the caller's request and is never replayed by a queue, and a
        // repeated request is a deliberate new confirmation link that must be mailed again.
        await _bus.InvokeAsync(new SendEmailCommand(
            cmd.NewEmail,
            account?.Language,
            EmailChangeTemplateSeeder.Slug,
            new EmailChangeTemplateModel(confirmationLink)), ct);

        return Result.Success();
    }
}
