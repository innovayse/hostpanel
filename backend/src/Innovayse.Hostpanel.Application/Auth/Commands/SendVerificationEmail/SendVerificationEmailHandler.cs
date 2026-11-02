using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Domain.Auth.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Auth.Commands.SendVerificationEmail;

/// <summary>
/// Handles <see cref="SendVerificationEmailCommand"/>: issues a confirmation token, seeds the
/// shared template on first use, builds the link into the app the person actually uses (the admin
/// panel for staff, the client portal for everyone else) and dispatches the mail.
/// </summary>
public sealed class SendVerificationEmailHandler
{
    /// <summary>Reads the person, for the address and whether it is already confirmed.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Issues the confirmation token, where this deployment owns the accounts.</summary>
    private readonly IUserProvisioning _provisioning;

    /// <summary>Reads the person's roles, which decide which app the link opens.</summary>
    private readonly ISubjectRoleStore _roles;

    /// <summary>The module's data context, for seeding the template.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine message bus, for sending the mail.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Where the client portal lives, for the link sent to clients.</summary>
    private readonly IOptions<ClientPortalOptions> _clientPortal;

    /// <summary>Where the admin panel lives, for the link sent to staff.</summary>
    private readonly IOptions<AdminPanelOptions> _adminPanel;

    /// <summary>Initializes the handler.</summary>
    /// <param name="identity">Reads the person, for the address and whether it is already confirmed.</param>
    /// <param name="provisioning">Issues the confirmation token, where this deployment owns the accounts.</param>
    /// <param name="roles">Reads the person's roles, which decide which app the link opens.</param>
    /// <param name="context">The module's data context, for seeding the template.</param>
    /// <param name="bus">Wolverine message bus, for sending the mail.</param>
    /// <param name="clientPortal">Where the client portal lives, for the link sent to clients.</param>
    /// <param name="adminPanel">Where the admin panel lives, for the link sent to staff.</param>
    public SendVerificationEmailHandler(
        IIdentityProvider identity,
        IUserProvisioning provisioning,
        ISubjectRoleStore roles,
        IHostpanelDbContext context,
        IMessageBus bus,
        IOptions<ClientPortalOptions> clientPortal,
        IOptions<AdminPanelOptions> adminPanel)
    {
        _identity = identity;
        _provisioning = provisioning;
        _roles = roles;
        _context = context;
        _bus = bus;
        _clientPortal = clientPortal;
        _adminPanel = adminPanel;
    }

    /// <summary>Issues the token and sends the verification mail.</summary>
    /// <param name="cmd">The account to verify.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success (also, without sending anything, when the address is already confirmed),
    /// <c>IDENTITY_USER_NOT_FOUND</c> when the subject names no account, or
    /// <c>USER_PROVISIONING_NOT_ALLOWED_ISSUE_EMAIL_CONFIRMATION</c> where an external sign-on provider owns the accounts.
    /// </returns>
    public async Task<Result> HandleAsync(SendVerificationEmailCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var account = await _identity.FindBySubjectAsync(cmd.AccountSubject, ct);
        if (account is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.IDENTITY_USER_NOT_FOUND);
        }

        // Nothing to verify: a second mail would only hand out a token that confirms nothing.
        if (account.EmailVerified == true)
        {
            return Result.Success();
        }

        // The token first: it is the half that refuses where an external sign-on provider owns the account, and
        // seeding a template and sending mail before finding that out would deliver a link
        // that confirms nothing.
        var tokenOutcome = await _provisioning.IssueEmailConfirmationTokenAsync(cmd.AccountSubject, ct);
        if (tokenOutcome.IsFailure)
        {
            return Result.FailureCode(tokenOutcome.ErrorCode!);
        }

        if (tokenOutcome.Value is null)
        {
            // Identity and the local store disagree: only a delete racing this lookup can cause it.
            return Result.FailureCode(HostpanelErrorCodes.IDENTITY_USER_NOT_FOUND);
        }

        var held = await _roles.GetRolesAsync(cmd.AccountSubject, ct);
        var isStaff = held.Contains(Role.Admin) || held.Contains(Role.Reseller);

        await EmailVerificationTemplateSeeder.EnsureSeededAsync(_context, ct);

        // The two apps do not share a landing page: both read "token" and "email" from the query
        // and post them to the confirmation endpoint, but the paths differ.
        var landing = isStaff
            ? $"{_adminPanel.Value.BaseUrl}/verify-email"
            : $"{_clientPortal.Value.BaseUrl}/client/confirm-email";
        var verificationLink =
            $"{landing}?token={Uri.EscapeDataString(tokenOutcome.Value)}&email={Uri.EscapeDataString(account.Email)}";

        // No idempotency key: this runs inside the caller's request and is never replayed by a queue, and a
        // repeated request is a deliberate new verification link that must be mailed again.
        await _bus.InvokeAsync(new SendEmailCommand(
            account.Email,
            account.Language,
            EmailVerificationTemplateSeeder.Slug,
            new EmailVerificationTemplateModel(verificationLink)), ct);

        return Result.Success();
    }
}
