using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Notifications.Seeding;

/// <summary>
/// Lazily seeds the shared "user-password-reset" email template, in every supported language, on
/// first use. Used by both the admin-initiated reset (<c>AdminUsersController</c>) and the
/// client/local self-service "forgot password" flow (<c>RequestPasswordResetHandler</c>)
/// so the two entry points send an identical email instead of drifting apart.
/// <para>
/// Each language version is seeded once and never overwritten; an install that already has it
/// keeps its stored HTML until the operator re-saves the template from the admin panel.
/// </para>
/// </summary>
public static class PasswordResetTemplateSeeder
{
    /// <summary>The shared template slug used by both reset entry points.</summary>
    public const string Slug = "user-password-reset";

    /// <summary>
    /// Ensures every language version of the password reset template exists, creating the missing
    /// ones with the default design.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once every version exists.</returns>
    public static Task EnsureSeededAsync(IHostpanelDbContext context, CancellationToken ct)
    {
        return EmailTemplateSeeding.AddMissingAsync(context, EmailTemplateSeeding.Branded(
            Slug,
            "Sent when a password reset is requested, whether by the user or an admin.",
            [
                new SeededEmailVersion("Reset your password | {{ site.name }}", new SeededEmailCopy(
                    "en",
                    "Reset Your Password",
                    ["Click the button below to reset your password. This link will expire in 24 hours."],
                    "{{ reset_link }}",
                    "Reset Password",
                    "If you didn't request this, you can safely ignore this email.",
                    EchoButtonUrl: true)),
                new SeededEmailVersion("Сброс пароля | {{ site.name }}", new SeededEmailCopy(
                    "ru",
                    "Сброс пароля",
                    ["Нажмите кнопку ниже, чтобы сбросить пароль. Ссылка действует 24 часа."],
                    "{{ reset_link }}",
                    "Сбросить пароль",
                    "Если вы не запрашивали сброс пароля, просто проигнорируйте это письмо.",
                    EchoButtonUrl: true)),
                new SeededEmailVersion("Գաղտնաբառի վերականգնում | {{ site.name }}", new SeededEmailCopy(
                    "hy",
                    "Գաղտնաբառի վերականգնում",
                    ["Սեղմեք ստորև կոճակը՝ գաղտնաբառը վերականգնելու համար։ Հղումը գործում է 24 ժամ։"],
                    "{{ reset_link }}",
                    "Վերականգնել գաղտնաբառը",
                    "Եթե դուք չեք խնդրել գաղտնաբառի վերականգնում, պարզապես անտեսեք այս նամակը։",
                    EchoButtonUrl: true)),
            ]), ct);
    }
}
