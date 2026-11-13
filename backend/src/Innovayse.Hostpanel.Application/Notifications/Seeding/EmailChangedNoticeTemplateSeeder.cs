using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Notifications.Seeding;

/// <summary>
/// Lazily seeds the "user-email-changed-notice" email template, in every supported language, on
/// first use: the notice the previous address gets once a client's sign-in address has changed.
/// <para>
/// Each language version is seeded once and never overwritten; an install that already has it
/// keeps its stored HTML until the operator re-saves the template from the admin panel.
/// </para>
/// </summary>
public static class EmailChangedNoticeTemplateSeeder
{
    /// <summary>The template slug the mail is sent under.</summary>
    public const string Slug = "user-email-changed-notice";

    /// <summary>
    /// Ensures every language version of the template exists, creating the missing ones with the
    /// default design the first time an address change takes effect.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once every version exists.</returns>
    public static Task EnsureSeededAsync(IHostpanelDbContext context, CancellationToken ct)
    {
        return EmailTemplateSeeding.AddMissingAsync(context, EmailTemplateSeeding.Branded(
            Slug,
            "Sent to the previous address once a client's sign-in address has changed.",
            [
                new SeededEmailVersion("Your sign-in email changed | {{ site.name }}", new SeededEmailCopy(
                    "en",
                    "Your Sign-in Email Changed",
                    ["Your account now signs in with {{ new_email }}. Every session that was open has been signed out."],
                    ButtonUrl: null,
                    ButtonText: null,
                    "If you did not make this change, contact support right away.",
                    EchoButtonUrl: false)),
                new SeededEmailVersion("Адрес для входа изменён | {{ site.name }}", new SeededEmailCopy(
                    "ru",
                    "Адрес для входа изменён",
                    ["Теперь для входа в аккаунт используется {{ new_email }}. Все открытые сеансы завершены."],
                    ButtonUrl: null,
                    ButtonText: null,
                    "Если это изменение сделали не вы, срочно обратитесь в поддержку.",
                    EchoButtonUrl: false)),
                new SeededEmailVersion("Մուտքի էլ. հասցեն փոխվել է | {{ site.name }}", new SeededEmailCopy(
                    "hy",
                    "Մուտքի էլ. հասցեն փոխվել է",
                    ["Այժմ ձեր հաշիվ մուտք է գործվում {{ new_email }} հասցեով։ Բոլոր բաց սեանսները փակվել են։"],
                    ButtonUrl: null,
                    ButtonText: null,
                    "Եթե այս փոփոխությունը դուք չեք կատարել, անմիջապես դիմեք աջակցության ծառայությանը։",
                    EchoButtonUrl: false)),
            ]), ct);
    }
}
