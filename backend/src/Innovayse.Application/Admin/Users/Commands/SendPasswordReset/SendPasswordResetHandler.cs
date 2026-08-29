namespace Innovayse.Application.Admin.Users.Commands.SendPasswordReset;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Common.Options;
using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Application.Notifications.Services;
using Innovayse.Domain.Notifications.Interfaces;
using Microsoft.Extensions.Options;
using Wolverine;

/// <summary>
/// Handles <see cref="SendPasswordResetCommand"/>: issues a reset token, seeds the shared
/// email template on first use, builds the reset link, and dispatches the mail.
/// </summary>
/// <param name="identity">Reads the person, for the address the mail goes to.</param>
/// <param name="provisioning">Issues the reset token, where this deployment owns the accounts.</param>
/// <param name="templateRepo">Email template repository, for seeding the reset template.</param>
/// <param name="uow">Unit of work for persisting the template if newly created.</param>
/// <param name="bus">Wolverine message bus, for sending the mail.</param>
/// <param name="clientPortal">Where the client portal lives, for the link in the reset mail.</param>
public sealed class SendPasswordResetHandler(
    IIdentityProvider identity,
    IUserProvisioning provisioning,
    IEmailTemplateRepository templateRepo,
    IUnitOfWork uow,
    IMessageBus bus,
    IOptions<ClientPortalOptions> clientPortal)
{
    /// <summary>Issues the token and sends the reset mail.</summary>
    /// <param name="cmd">The subject to send the reset link to.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the user is not found.</exception>
    /// <exception cref="UserProvisioningNotAllowedException">
    /// Where an SSO owns the accounts.
    /// </exception>
    public async Task HandleAsync(SendPasswordResetCommand cmd, CancellationToken ct)
    {
        var account = await identity.FindBySubjectAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"User {cmd.Id} not found.");

        // The token first: it is the half that refuses where an SSO owns the account, and
        // seeding a template and sending mail before finding that out would leave an
        // operator with a delivered reset link that resets nothing.
        var token = await provisioning.IssuePasswordResetTokenAsync(cmd.Id, ct);

        await PasswordResetTemplateSeeder.EnsureSeededAsync(templateRepo, uow, ct);

        var clientBaseUrl = clientPortal.Value.BaseUrl;
        var resetLink = $"{clientBaseUrl}/client/reset-password?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(account.Email)}";

        await bus.InvokeAsync(new SendEmailCommand(
            account.Email,
            PasswordResetTemplateSeeder.Slug,
            new { reset_link = resetLink }), ct);
    }
}
