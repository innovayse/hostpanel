using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Notifications.Seeding;

/// <summary>
/// Lazily seeds the "user-invite" email template, in every supported language, on first use: the
/// mail a person receives when they are invited to manage a client account. Each language version
/// is seeded once and never overwritten.
/// </summary>
public static class UserInviteTemplateSeeder
{
    /// <summary>The template slug the invitation is sent with.</summary>
    public const string Slug = "user-invite";

    /// <summary>The highlighted client name, styled the same in every language.</summary>
    private const string ClientName = """<strong style="color:#f0f0f5;">{{ client_name }}</strong>""";

    /// <summary>Ensures every language version of the invitation template exists, creating the missing ones on first use.</summary>
    /// <param name="context">The data context the template rows live in.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once every version exists.</returns>
    public static Task EnsureSeededAsync(IHostpanelDbContext context, CancellationToken ct)
    {
        return EmailTemplateSeeding.AddMissingAsync(context, EmailTemplateSeeding.Branded(
            Slug,
            "Sent when a user is invited to manage a client account.",
            [
                new SeededEmailVersion("You've been invited to {{ site.name }}", new SeededEmailCopy(
                    "en",
                    "You've Been Invited!",
                    [
                        "Hi {{ first_name }},",
                        $"You've been invited to manage the account for {ClientName}.",
                        "Click the button below to set your password and get started:",
                    ],
                    "{{ invite_link }}",
                    "Set Your Password",
                    "This link expires in 7 days. If you didn't expect this invitation, you can safely ignore this email.",
                    EchoButtonUrl: true)),
                new SeededEmailVersion("Вас пригласили в {{ site.name }}", new SeededEmailCopy(
                    "ru",
                    "Вас пригласили!",
                    [
                        "Здравствуйте, {{ first_name }}!",
                        $"Вас пригласили управлять аккаунтом {ClientName}.",
                        "Нажмите кнопку ниже, чтобы задать пароль и начать работу:",
                    ],
                    "{{ invite_link }}",
                    "Задать пароль",
                    "Ссылка действует 7 дней. Если вы не ждали этого приглашения, просто проигнорируйте это письмо.",
                    EchoButtonUrl: true)),
                new SeededEmailVersion("Ձեզ հրավիրել են {{ site.name }}", new SeededEmailCopy(
                    "hy",
                    "Ձեզ հրավիրել են",
                    [
                        "Բարև, {{ first_name }},",
                        $"Ձեզ հրավիրել են կառավարելու {ClientName} հաշիվը։",
                        "Սեղմեք ստորև կոճակը՝ գաղտնաբառ սահմանելու և սկսելու համար։",
                    ],
                    "{{ invite_link }}",
                    "Սահմանել գաղտնաբառ",
                    "Հղումը գործում է 7 օր։ Եթե չէիք սպասում այս հրավերին, պարզապես անտեսեք այս նամակը։",
                    EchoButtonUrl: true)),
                new SeededEmailVersion("Sie wurden zu {{ site.name }} eingeladen", new SeededEmailCopy(
                    "de",
                    "Sie wurden eingeladen!",
                    [
                        "Guten Tag {{ first_name }},",
                        $"Sie wurden eingeladen, das Konto von {ClientName} zu verwalten.",
                        "Klicken Sie auf die Schaltfläche unten, um Ihr Passwort festzulegen und loszulegen:",
                    ],
                    "{{ invite_link }}",
                    "Passwort festlegen",
                    "Dieser Link ist 7 Tage gültig. Wenn Sie diese Einladung nicht erwartet haben, können Sie diese E-Mail einfach ignorieren.",
                    EchoButtonUrl: true)),
                new SeededEmailVersion("Ha sido invitado a {{ site.name }}", new SeededEmailCopy(
                    "es",
                    "¡Ha sido invitado!",
                    [
                        "Hola, {{ first_name }}:",
                        $"Ha sido invitado a administrar la cuenta de {ClientName}.",
                        "Haga clic en el botón de abajo para establecer su contraseña y comenzar:",
                    ],
                    "{{ invite_link }}",
                    "Establecer contraseña",
                    "Este enlace caduca en 7 días. Si no esperaba esta invitación, puede ignorar este correo electrónico.",
                    EchoButtonUrl: true)),
            ]), ct);
    }
}
