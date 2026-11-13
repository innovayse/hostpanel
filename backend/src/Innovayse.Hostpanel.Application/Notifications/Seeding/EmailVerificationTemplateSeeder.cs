using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Notifications.Seeding;

/// <summary>
/// Lazily seeds the "user-email-verification" email template, in every supported language, the
/// first time a verification mail is requested. Each language version is seeded once and never
/// overwritten, so an operator's edit survives.
/// </summary>
public static class EmailVerificationTemplateSeeder
{
    /// <summary>The template slug the verification mail is sent under.</summary>
    public const string Slug = "user-email-verification";

    /// <summary>
    /// Ensures every language version of the email verification template exists, creating the
    /// missing ones with the default design.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once every version exists.</returns>
    public static Task EnsureSeededAsync(IHostpanelDbContext context, CancellationToken ct)
    {
        return EmailTemplateSeeding.AddMissingAsync(context, EmailTemplateSeeding.Branded(
            Slug,
            "Sent after a local registration, and when a user asks for the verification link again.",
            [
                new SeededEmailVersion("Verify your email | {{ site.name }}", new SeededEmailCopy(
                    "en",
                    "Verify Your Email",
                    ["Click the button below to confirm your email address and finish setting up your account."],
                    "{{ verification_link }}",
                    "Verify Email",
                    "If you didn't create this account, you can safely ignore this email.",
                    EchoButtonUrl: true)),
                new SeededEmailVersion("Подтвердите email | {{ site.name }}", new SeededEmailCopy(
                    "ru",
                    "Подтвердите email",
                    ["Нажмите кнопку ниже, чтобы подтвердить адрес электронной почты и завершить настройку аккаунта."],
                    "{{ verification_link }}",
                    "Подтвердить email",
                    "Если вы не создавали этот аккаунт, просто проигнорируйте это письмо.",
                    EchoButtonUrl: true)),
                new SeededEmailVersion("Հաստատեք ձեր էլ. հասցեն | {{ site.name }}", new SeededEmailCopy(
                    "hy",
                    "Հաստատեք ձեր էլ. հասցեն",
                    ["Սեղմեք ստորև կոճակը՝ ձեր էլ. հասցեն հաստատելու և հաշվի կարգավորումն ավարտելու համար։"],
                    "{{ verification_link }}",
                    "Հաստատել հասցեն",
                    "Եթե դուք չեք ստեղծել այս հաշիվը, պարզապես անտեսեք այս նամակը։",
                    EchoButtonUrl: true)),
            ]), ct);
    }
}
