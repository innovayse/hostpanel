namespace Innovayse.Application.Clients.Commands.InviteUserToClient;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Notifications;
using Innovayse.Domain.Notifications.Interfaces;
using Microsoft.Extensions.Configuration;
using Wolverine;

/// <summary>
/// Handles <see cref="InviteUserToClientCommand"/>.
/// Validates the invitation preconditions, creates an <see cref="Invitation"/> entity,
/// seeds the email template on first use, and sends the invitation email.
/// </summary>
/// <param name="clientRepo">Client repository for loading the client account.</param>
/// <param name="invitationRepo">Invitation repository for persistence and duplicate checks.</param>
/// <param name="userService">Identity user service for owner and duplicate user checks.</param>
/// <param name="templateRepo">Email template repository for seeding the invite template.</param>
/// <param name="uow">Unit of work for persisting changes.</param>
/// <param name="bus">Wolverine message bus for sending the invitation email.</param>
/// <param name="configuration">Application configuration for reading the client base URL.</param>
public sealed class InviteUserToClientHandler(
    IClientRepository clientRepo,
    IInvitationRepository invitationRepo,
    IUserService userService,
    IEmailTemplateRepository templateRepo,
    IUnitOfWork uow,
    IMessageBus bus,
    IConfiguration configuration)
{
    /// <summary>The email template slug used for user invitations.</summary>
    private const string TemplateSlug = "user-invite";

    /// <summary>
    /// Invites a user to a client account by creating an invitation and sending an email.
    /// </summary>
    /// <param name="cmd">The invite command with client ID, email, name, and permissions.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the client is not found, the email matches the owner,
    /// the user is already linked, or a pending invitation already exists.
    /// </exception>
    public async Task HandleAsync(InviteUserToClientCommand cmd, CancellationToken ct)
    {
        var client = await clientRepo.FindByIdAsync(cmd.ClientId, ct)
            ?? throw new InvalidOperationException($"Client {cmd.ClientId} not found.");

        // Check if the email matches the account owner
        var owner = await userService.FindByIdAsync(client.UserId, ct)
            ?? throw new InvalidOperationException($"Owner user {client.UserId} not found.");

        if (string.Equals(owner.Email, cmd.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot invite the account owner as an additional user.");
        }

        // Check if a user with this email is already linked as an additional user
        var matchingUserIds = await userService.FindUserIdsByEmailAsync(cmd.Email, ct);
        foreach (var userId in matchingUserIds)
        {
            if (client.Users.Any(u => u.UserId == userId))
            {
                throw new InvalidOperationException($"A user with email '{cmd.Email}' is already linked to this client.");
            }
        }

        // Check for existing pending invitation
        var pending = await invitationRepo.FindPendingByEmailAndClientAsync(cmd.Email, cmd.ClientId, ct);
        if (pending is not null)
        {
            throw new InvalidOperationException($"A pending invitation for '{cmd.Email}' already exists for this client.");
        }

        // Validate permissions value before casting
        if ((cmd.Permissions & ~(int)ClientPermission.All) != 0)
            throw new InvalidOperationException($"Invalid permissions value: {cmd.Permissions}.");

        // Create and persist the invitation
        var invitation = Invitation.Create(cmd.ClientId, cmd.Email, cmd.FirstName, cmd.LastName, (ClientPermission)cmd.Permissions);
        invitationRepo.Add(invitation);
        await uow.SaveChangesAsync(ct);

        // Seed the invite email template on first use
        await SeedInviteTemplateAsync(ct);

        // Build invite link and send email
        var clientBaseUrl = configuration["ClientBaseUrl"] ?? "http://localhost:3000";
        var inviteLink = $"{clientBaseUrl}/client/accept-invite?token={Uri.EscapeDataString(invitation.Token)}";

        await bus.InvokeAsync(new SendEmailCommand(
            cmd.Email,
            TemplateSlug,
            new
            {
                invite_link = inviteLink,
                client_name = $"{client.FirstName} {client.LastName}",
                first_name = cmd.FirstName
            }), ct);
    }

    /// <summary>
    /// Seeds the user invitation email template if it does not yet exist in the database.
    /// Uses the same dark-theme styling as the password reset template.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    private async Task SeedInviteTemplateAsync(CancellationToken ct)
    {
        var existing = await templateRepo.FindBySlugAsync(TemplateSlug, ct);
        if (existing is not null)
        {
            return;
        }

        var body = """
            <!DOCTYPE html>
            <html>
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1.0"></head>
            <body style="margin:0;padding:0;background-color:#0a0a0f;font-family:'Inter',system-ui,-apple-system,sans-serif;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#0a0a0f;padding:40px 20px;">
                <tr><td align="center">
                  <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="max-width:480px;width:100%;">
                    <tr><td align="center" style="padding-bottom:32px;">
                      <table role="presentation" cellpadding="0" cellspacing="0"><tr>
                        <td style="background:linear-gradient(135deg,rgba(14,165,233,0.1),rgba(168,85,247,0.1));border:1px solid rgba(14,165,233,0.2);border-radius:10px;padding:8px 16px;">
                          <span style="font-size:16px;font-weight:700;background:linear-gradient(135deg,#0ea5e9,#a855f7);-webkit-background-clip:text;-webkit-text-fill-color:transparent;background-clip:text;">Innovayse</span>
                        </td>
                      </tr></table>
                    </td></tr>
                    <tr><td style="background-color:#1a1a1f;border:1px solid rgba(255,255,255,0.06);border-radius:16px;padding:40px 36px;">
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                        <tr><td align="center" style="padding-bottom:24px;">
                          <h1 style="margin:0;font-size:22px;font-weight:700;color:#f0f0f5;">You've Been Invited!</h1>
                        </td></tr>
                        <tr><td align="center" style="padding-bottom:12px;">
                          <p style="margin:0;font-size:14px;color:#8a8a9a;line-height:1.6;">Hi {{ first_name }},</p>
                        </td></tr>
                        <tr><td align="center" style="padding-bottom:28px;">
                          <p style="margin:0;font-size:14px;color:#8a8a9a;line-height:1.6;">You've been invited to manage the account for <strong style="color:#f0f0f5;">{{ client_name }}</strong>.</p>
                          <p style="margin:8px 0 0;font-size:14px;color:#8a8a9a;line-height:1.6;">Click the button below to set your password and get started:</p>
                        </td></tr>
                        <tr><td align="center" style="padding-bottom:28px;">
                          <a href="{{ invite_link }}" style="display:inline-block;padding:14px 32px;background:linear-gradient(135deg,#0ea5e9,#0284c7);color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;border-radius:10px;box-shadow:0 4px 20px rgba(14,165,233,0.25);">Set Your Password</a>
                        </td></tr>
                        <tr><td style="border-top:1px solid rgba(255,255,255,0.06);padding-top:20px;">
                          <p style="margin:0;font-size:12px;color:#5a5a6a;line-height:1.6;">This link expires in 7 days. If you didn't expect this invitation, you can safely ignore this email.</p>
                          <p style="margin:8px 0 0;font-size:12px;color:#0ea5e9;word-break:break-all;">{{ invite_link }}</p>
                        </td></tr>
                      </table>
                    </td></tr>
                    <tr><td align="center" style="padding-top:24px;">
                      <p style="margin:0;font-size:11px;color:#3a3a4a;">© Innovayse. All rights reserved.</p>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;

        var template = EmailTemplate.Create(TemplateSlug, "You've been invited — Innovayse", body,
            "Sent when a user is invited to manage a client account.");
        templateRepo.Add(template);
        await uow.SaveChangesAsync(ct);
    }
}
