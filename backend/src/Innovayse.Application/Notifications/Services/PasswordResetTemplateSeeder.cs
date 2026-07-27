namespace Innovayse.Application.Notifications.Services;

using Innovayse.Application.Common;
using Innovayse.Domain.Notifications;
using Innovayse.Domain.Notifications.Interfaces;

/// <summary>
/// Lazily seeds the shared "user-password-reset" email template on first use.
/// Used by both the admin-initiated reset (<c>AdminUsersController</c>) and the
/// client/local self-service "forgot password" flow (<c>LocalAuthController</c>)
/// so the two entry points send an identical email instead of drifting apart.
/// </summary>
public static class PasswordResetTemplateSeeder
{
    /// <summary>The shared template slug used by both reset entry points.</summary>
    public const string Slug = "user-password-reset";

    /// <summary>
    /// Ensures the password reset template exists, creating it with the default
    /// design if this is the first reset ever requested.
    /// </summary>
    /// <param name="templateRepo">Email template repository.</param>
    /// <param name="uow">Unit of work for persisting the template if newly created.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task EnsureSeededAsync(
        IEmailTemplateRepository templateRepo,
        IUnitOfWork uow,
        CancellationToken ct)
    {
        var existing = await templateRepo.FindBySlugAsync(Slug, ct);
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
                          <h1 style="margin:0;font-size:22px;font-weight:700;color:#f0f0f5;">Reset Your Password</h1>
                        </td></tr>
                        <tr><td align="center" style="padding-bottom:28px;">
                          <p style="margin:0;font-size:14px;color:#8a8a9a;line-height:1.6;">Click the button below to reset your password. This link will expire in 24 hours.</p>
                        </td></tr>
                        <tr><td align="center" style="padding-bottom:28px;">
                          <a href="{{ reset_link }}" style="display:inline-block;padding:14px 32px;background:linear-gradient(135deg,#0ea5e9,#0284c7);color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;border-radius:10px;box-shadow:0 4px 20px rgba(14,165,233,0.25);">Reset Password</a>
                        </td></tr>
                        <tr><td style="border-top:1px solid rgba(255,255,255,0.06);padding-top:20px;">
                          <p style="margin:0;font-size:12px;color:#5a5a6a;line-height:1.6;">If you didn't request this, you can safely ignore this email.</p>
                          <p style="margin:8px 0 0;font-size:12px;color:#0ea5e9;word-break:break-all;">{{ reset_link }}</p>
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

        var template = EmailTemplate.Create(Slug, "Reset your password — Innovayse", body,
            "Sent when a password reset is requested, whether by the user or an admin.");
        templateRepo.Add(template);
        await uow.SaveChangesAsync(ct);
    }
}
