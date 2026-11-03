using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Auth.Commands.RequestPasswordReset;

/// <summary>
/// Handles <see cref="RequestPasswordResetCommand"/>: finds the account holding the address, issues
/// a reset token, seeds the shared email template on first use, builds the reset link and
/// dispatches the mail. An address no account holds ends quietly.
/// </summary>
public sealed class RequestPasswordResetHandler
{
    /// <summary>Finds the account holding the address.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Issues the reset token, where this deployment owns the accounts.</summary>
    private readonly IUserProvisioning _provisioning;

    /// <summary>The module's data context, for seeding the reset template.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine message bus, for sending the mail.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Where the client portal lives, for the link in the reset mail.</summary>
    private readonly IOptions<ClientPortalOptions> _clientPortal;

    /// <summary>Initializes the handler.</summary>
    /// <param name="identity">Finds the account holding the address.</param>
    /// <param name="provisioning">Issues the reset token, where this deployment owns the accounts.</param>
    /// <param name="context">The module's data context, for seeding the reset template.</param>
    /// <param name="bus">Wolverine message bus, for sending the mail.</param>
    /// <param name="clientPortal">Where the client portal lives, for the link in the reset mail.</param>
    public RequestPasswordResetHandler(
        IIdentityProvider identity,
        IUserProvisioning provisioning,
        IHostpanelDbContext context,
        IMessageBus bus,
        IOptions<ClientPortalOptions> clientPortal)
    {
        _identity = identity;
        _provisioning = provisioning;
        _context = context;
        _bus = bus;
        _clientPortal = clientPortal;
    }

    /// <summary>Issues the token and sends the reset mail when an account holds the address.</summary>
    /// <param name="cmd">The address the reset link is asked for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, also when no account holds the address, or
    /// <c>USER_PROVISIONING_NOT_ALLOWED_ISSUE_PASSWORD_RESET</c> where an external sign-on provider owns the accounts.
    /// </returns>
    public async Task<Result> HandleAsync(RequestPasswordResetCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (string.IsNullOrWhiteSpace(cmd.Email))
        {
            return Result.Success();
        }

        var account = await _identity.FindByEmailAsync(cmd.Email, ct);
        if (account is null)
        {
            return Result.Success();
        }

        // The token first: it is the half that refuses where an external sign-on provider owns the
        // account, and seeding a template and sending mail before finding that out would deliver a
        // reset link that resets nothing.
        var tokenOutcome = await _provisioning.IssuePasswordResetTokenAsync(account.Subject, ct);
        if (tokenOutcome.IsFailure)
        {
            return Result.FailureCode(tokenOutcome.ErrorCode!);
        }

        if (tokenOutcome.Value is null)
        {
            // The identity provider and the local account store disagree, which only a race
            // between this lookup and a delete elsewhere in the same instant could cause.
            return Result.FailureCode(HostpanelErrorCodes.IDENTITY_USER_NOT_FOUND);
        }

        await PasswordResetTemplateSeeder.EnsureSeededAsync(_context, ct);

        var encodedToken = Uri.EscapeDataString(tokenOutcome.Value);
        var encodedEmail = Uri.EscapeDataString(account.Email);
        var resetLink = $"{_clientPortal.Value.BaseUrl}/client/reset-password?token={encodedToken}&email={encodedEmail}";

        // No idempotency key: this runs inside the caller's request and is never replayed by a queue, and a
        // repeated request is a deliberate new reset link that must be mailed again.
        await _bus.InvokeAsync(new SendEmailCommand(
            account.Email,
            account.Language,
            PasswordResetTemplateSeeder.Slug,
            new PasswordResetTemplateModel(resetLink)), ct);

        return Result.Success();
    }
}
