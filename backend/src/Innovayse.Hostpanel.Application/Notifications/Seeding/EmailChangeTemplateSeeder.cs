using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Notifications.Seeding;

/// <summary>
/// Lazily seeds the "user-email-change" email template, in every supported language, on first use:
/// the link a client follows from their new address to make an address change effective.
/// <para>
/// Each language version is seeded once and never overwritten; an install that already has it
/// keeps its stored HTML until the operator re-saves the template from the admin panel.
/// </para>
/// </summary>
public static class EmailChangeTemplateSeeder
{
    /// <summary>The template slug the mail is sent under.</summary>
    public const string Slug = "user-email-change";

    /// <summary>
    /// Ensures every language version of the template exists, creating the missing ones with the
    /// default design the first time an address change is asked for.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once every version exists.</returns>
    public static Task EnsureSeededAsync(IHostpanelDbContext context, CancellationToken ct)
    {
        return EmailTemplateSeeding.AddMissingAsync(context, EmailTemplateSeeding.Branded(
            Slug,
            "Sent to the new address when a client asks to change the address they sign in with.",
            [
                new SeededEmailVersion("Confirm your new email | {{ site.name }}", new SeededEmailCopy(
                    "en",
                    "Confirm Your New Email",
                    ["Click the button below to start signing in with this address. Until you do, your account keeps its current address."],
                    "{{ confirmation_link }}",
                    "Confirm Email",
                    "If you did not ask for this change, you can safely ignore this email.",
                    EchoButtonUrl: true)),
                new SeededEmailVersion("Подтвердите новый email | {{ site.name }}", new SeededEmailCopy(
                    "ru",
                    "Подтвердите новый email",
                    ["Нажмите кнопку ниже, чтобы входить с этим адресом. Пока вы этого не сделаете, у аккаунта останется текущий адрес."],
                    "{{ confirmation_link }}",
                    "Подтвердить email",
                    "Если вы не запрашивали это изменение, просто проигнорируйте это письмо.",
                    EchoButtonUrl: true)),
                new SeededEmailVersion("Հաստատեք նոր էլ. հասցեն | {{ site.name }}", new SeededEmailCopy(
                    "hy",
                    "Հաստատեք նոր էլ. հասցեն",
                    ["Սեղմեք ստորև կոճակը՝ այս հասցեով մուտք գործելու համար։ Մինչ այդ ձեր հաշիվը պահպանում է ներկայիս հասցեն։"],
                    "{{ confirmation_link }}",
                    "Հաստատել հասցեն",
                    "Եթե դուք չեք խնդրել այս փոփոխությունը, պարզապես անտեսեք այս նամակը։",
                    EchoButtonUrl: true)),
            ]), ct);
    }
}
