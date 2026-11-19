using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Notifications;

namespace Innovayse.Hostpanel.Application.Notifications.Seeding;

/// <summary>
/// Seeds the seven domain mails the domain handlers send, in every supported language, at startup
/// (the pattern of <see cref="TransactionalTemplatesSeeder"/>): without a stored template the send
/// logs "template not found" and nothing leaves. A version already stored, edited or not, is never
/// touched, so an operator's copy survives every restart.
/// </summary>
public static class DomainTemplatesSeeder
{
    /// <summary>Slug of the mail that tells a client a domain was renewed.</summary>
    public const string RenewedSlug = "domain-renewed";

    /// <summary>Slug of the mail that warns a client a domain is about to expire.</summary>
    public const string ExpiringSlug = "domain-expiring";

    /// <summary>Slug of the mail that tells a client a domain registration failed.</summary>
    public const string RegistrationFailedSlug = "domain-registration-failed";

    /// <summary>Slug of the staff alert about a failed domain registration.</summary>
    public const string RegistrationFailedAdminSlug = "domain-registration-failed-admin";

    /// <summary>Slug of the mail that tells a client a domain was registered.</summary>
    public const string RegisteredSlug = "domain-registered";

    /// <summary>Slug of the mail that tells a client a domain expired.</summary>
    public const string ExpiredSlug = "domain-expired";

    /// <summary>Slug of the mail that tells a client a domain was transferred in.</summary>
    public const string TransferredSlug = "domain-transferred";

    /// <summary>Adds whichever language version of the seven templates is missing.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once every version exists.</returns>
    public static Task EnsureSeededAsync(IHostpanelDbContext context, CancellationToken ct)
    {
        return EmailTemplateSeeding.AddMissingAsync(
            context,
            [.. Renewed(), .. Expiring(), .. RegistrationFailed(), .. RegistrationFailedAdmin(), .. Registered(), .. Expired(), .. Transferred()],
            ct);
    }

    /// <summary>The renewal confirmation for a client, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> Renewed() => EmailTemplateSeeding.Branded(
        RenewedSlug,
        "Sent to the client when a domain has been renewed.",
        [
            new SeededEmailVersion("{{ domain.name }} has been renewed | {{ site.name }}", new SeededEmailCopy(
                "en",
                "Your domain was renewed",
                ["{{ domain.name }} has been renewed. It is now registered until {{ domain.new_expires_at }}."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Домен {{ domain.name }} продлён | {{ site.name }}", new SeededEmailCopy(
                "ru",
                "Ваш домен продлён",
                ["Домен {{ domain.name }} продлён. Теперь он зарегистрирован до {{ domain.new_expires_at }}."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("{{ domain.name }} դոմենը երկարաձգվել է | {{ site.name }}", new SeededEmailCopy(
                "hy",
                "Ձեր դոմենը երկարաձգվել է",
                ["{{ domain.name }} դոմենը երկարաձգվել է։ Այն այժմ գրանցված է մինչև {{ domain.new_expires_at }}։"],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("{{ domain.name }} wurde verlängert | {{ site.name }}", new SeededEmailCopy(
                "de",
                "Ihre Domain wurde verlängert",
                ["{{ domain.name }} wurde verlängert. Sie ist nun bis zum {{ domain.new_expires_at }} registriert."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("{{ domain.name }} ha sido renovado | {{ site.name }}", new SeededEmailCopy(
                "es",
                "Su dominio ha sido renovado",
                ["{{ domain.name }} ha sido renovado. Ahora está registrado hasta el {{ domain.new_expires_at }}."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
        ]);

    /// <summary>The expiry warning for a client, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> Expiring() => EmailTemplateSeeding.Branded(
        ExpiringSlug,
        "Sent to the client before a domain expires, on each warning threshold.",
        [
            new SeededEmailVersion("{{ domain.name }} expires in {{ domain.days_until_expiry }} days | {{ site.name }}", new SeededEmailCopy(
                "en",
                "Your domain is about to expire",
                ["{{ domain.name }} expires on {{ domain.expires_at }}, in {{ domain.days_until_expiry }} days. Renew it to keep your website and email working."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Домен {{ domain.name }} истекает через {{ domain.days_until_expiry }} дн. | {{ site.name }}", new SeededEmailCopy(
                "ru",
                "Срок действия домена скоро истекает",
                ["Домен {{ domain.name }} истекает {{ domain.expires_at }}, через {{ domain.days_until_expiry }} дн. Продлите его, чтобы сайт и почта продолжали работать."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("{{ domain.name }} դոմենը լրանում է {{ domain.days_until_expiry }} օրից | {{ site.name }}", new SeededEmailCopy(
                "hy",
                "Ձեր դոմենի ժամկետը շուտով լրանում է",
                ["{{ domain.name }} դոմենի ժամկետը լրանում է {{ domain.expires_at }}-ին՝ {{ domain.days_until_expiry }} օրից։ Երկարաձգեք այն, որպեսզի կայքը և էլ. փոստը շարունակեն աշխատել։"],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("{{ domain.name }} läuft in {{ domain.days_until_expiry }} Tagen ab | {{ site.name }}", new SeededEmailCopy(
                "de",
                "Ihre Domain läuft bald ab",
                ["{{ domain.name }} läuft am {{ domain.expires_at }} ab, also in {{ domain.days_until_expiry }} Tagen. Verlängern Sie sie, damit Ihre Website und Ihre E-Mails weiter funktionieren."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("{{ domain.name }} vence en {{ domain.days_until_expiry }} días | {{ site.name }}", new SeededEmailCopy(
                "es",
                "Su dominio está a punto de vencer",
                ["{{ domain.name }} vence el {{ domain.expires_at }}, dentro de {{ domain.days_until_expiry }} días. Renuévelo para que su sitio web y su correo electrónico sigan funcionando."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
        ]);

    /// <summary>The registration failure notice for a client, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> RegistrationFailed() => EmailTemplateSeeding.Branded(
        RegistrationFailedSlug,
        "Sent to the client when a paid domain registration could not be completed.",
        [
            new SeededEmailVersion("We could not register {{ domain.name }} | {{ site.name }}", new SeededEmailCopy(
                "en",
                "Domain registration failed",
                ["We could not complete the registration of {{ domain.name }}. Our team has been notified and will contact you about invoice #{{ invoice.id }}."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Не удалось зарегистрировать {{ domain.name }} | {{ site.name }}", new SeededEmailCopy(
                "ru",
                "Не удалось зарегистрировать домен",
                ["Нам не удалось завершить регистрацию домена {{ domain.name }}. Наша команда уже уведомлена и свяжется с вами по счёту №{{ invoice.id }}."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Չհաջողվեց գրանցել {{ domain.name }} դոմենը | {{ site.name }}", new SeededEmailCopy(
                "hy",
                "Դոմենի գրանցումը ձախողվեց",
                ["Մենք չկարողացանք ավարտել {{ domain.name }} դոմենի գրանցումը։ Մեր թիմը տեղեկացված է և կկապվի ձեզ հետ №{{ invoice.id }} հաշվի հարցով։"],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("{{ domain.name }} konnte nicht registriert werden | {{ site.name }}", new SeededEmailCopy(
                "de",
                "Domain-Registrierung fehlgeschlagen",
                ["Wir konnten die Registrierung von {{ domain.name }} nicht abschließen. Unser Team wurde informiert und wird sich wegen der Rechnung #{{ invoice.id }} bei Ihnen melden."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("No pudimos registrar {{ domain.name }} | {{ site.name }}", new SeededEmailCopy(
                "es",
                "Error en el registro del dominio",
                ["No pudimos completar el registro de {{ domain.name }}. Nuestro equipo ha sido notificado y se pondrá en contacto con usted respecto a la factura n.º {{ invoice.id }}."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
        ]);

    /// <summary>The registration failure alert for staff, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> RegistrationFailedAdmin() => EmailTemplateSeeding.Branded(
        RegistrationFailedAdminSlug,
        "Sent to staff when a paid domain registration could not be completed.",
        [
            new SeededEmailVersion("Domain registration failed: {{ domain.name }}", new SeededEmailCopy(
                "en",
                "Domain registration failed",
                ["{{ domain.name }} (client #{{ client.id }}, invoice #{{ invoice.id }}): {{ reason }}"],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Не удалась регистрация домена: {{ domain.name }}", new SeededEmailCopy(
                "ru",
                "Не удалась регистрация домена",
                ["{{ domain.name }} (клиент №{{ client.id }}, счёт №{{ invoice.id }}): {{ reason }}"],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Դոմենի գրանցումը ձախողվեց՝ {{ domain.name }}", new SeededEmailCopy(
                "hy",
                "Դոմենի գրանցումը ձախողվեց",
                ["{{ domain.name }} (հաճախորդ №{{ client.id }}, հաշիվ №{{ invoice.id }}): {{ reason }}"],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Domain-Registrierung fehlgeschlagen: {{ domain.name }}", new SeededEmailCopy(
                "de",
                "Domain-Registrierung fehlgeschlagen",
                ["{{ domain.name }} (Kunde #{{ client.id }}, Rechnung #{{ invoice.id }}): {{ reason }}"],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Error en el registro del dominio: {{ domain.name }}", new SeededEmailCopy(
                "es",
                "Error en el registro del dominio",
                ["{{ domain.name }} (cliente n.º {{ client.id }}, factura n.º {{ invoice.id }}): {{ reason }}"],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
        ]);

    /// <summary>The registration confirmation for a client, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> Registered() => EmailTemplateSeeding.Branded(
        RegisteredSlug,
        "Sent to the client when a domain has been registered.",
        [
            new SeededEmailVersion("{{ domain.name }} has been registered | {{ site.name }}", new SeededEmailCopy(
                "en",
                "Your domain was registered",
                ["{{ domain.name }} has been registered. It is registered until {{ domain.expires_at }}."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Домен {{ domain.name }} зарегистрирован | {{ site.name }}", new SeededEmailCopy(
                "ru",
                "Ваш домен зарегистрирован",
                ["Домен {{ domain.name }} зарегистрирован. Он действует до {{ domain.expires_at }}."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("{{ domain.name }} դոմենը գրանցվել է | {{ site.name }}", new SeededEmailCopy(
                "hy",
                "Ձեր դոմենը գրանցվել է",
                ["{{ domain.name }} դոմենը գրանցվել է։ Այն գրանցված է մինչև {{ domain.expires_at }}։"],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("{{ domain.name }} wurde registriert | {{ site.name }}", new SeededEmailCopy(
                "de",
                "Ihre Domain wurde registriert",
                ["{{ domain.name }} wurde registriert. Die Registrierung gilt bis zum {{ domain.expires_at }}."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("{{ domain.name }} ha sido registrado | {{ site.name }}", new SeededEmailCopy(
                "es",
                "Su dominio ha sido registrado",
                ["{{ domain.name }} ha sido registrado. El registro es válido hasta el {{ domain.expires_at }}."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
        ]);

    /// <summary>The expiry notice for a client, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> Expired() => EmailTemplateSeeding.Branded(
        ExpiredSlug,
        "Sent to the client when a domain has expired.",
        [
            new SeededEmailVersion("{{ domain.name }} has expired | {{ site.name }}", new SeededEmailCopy(
                "en",
                "Your domain has expired",
                ["{{ domain.name }} has expired. Renew it as soon as possible to bring your website and email back."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Срок действия домена {{ domain.name }} истёк | {{ site.name }}", new SeededEmailCopy(
                "ru",
                "Срок действия домена истёк",
                ["Срок действия домена {{ domain.name }} истёк. Продлите его как можно скорее, чтобы сайт и почта снова заработали."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("{{ domain.name }} դոմենի ժամկետը լրացել է | {{ site.name }}", new SeededEmailCopy(
                "hy",
                "Ձեր դոմենի ժամկետը լրացել է",
                ["{{ domain.name }} դոմենի ժամկետը լրացել է։ Երկարաձգեք այն հնարավորինս շուտ, որպեսզի կայքը և էլ. փոստը նորից աշխատեն։"],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("{{ domain.name }} ist abgelaufen | {{ site.name }}", new SeededEmailCopy(
                "de",
                "Ihre Domain ist abgelaufen",
                ["{{ domain.name }} ist abgelaufen. Verlängern Sie sie so schnell wie möglich, damit Ihre Website und Ihre E-Mails wieder funktionieren."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("{{ domain.name }} ha vencido | {{ site.name }}", new SeededEmailCopy(
                "es",
                "Su dominio ha vencido",
                ["{{ domain.name }} ha vencido. Renuévelo lo antes posible para que su sitio web y su correo electrónico vuelvan a funcionar."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
        ]);

    /// <summary>The incoming transfer confirmation for a client, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> Transferred() => EmailTemplateSeeding.Branded(
        TransferredSlug,
        "Sent to the client when a domain has been transferred in.",
        [
            new SeededEmailVersion("{{ domain.name }} has been transferred | {{ site.name }}", new SeededEmailCopy(
                "en",
                "Your domain was transferred",
                ["{{ domain.name }} has been transferred to us. It is registered until {{ domain.expires_at }}."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Домен {{ domain.name }} перенесён | {{ site.name }}", new SeededEmailCopy(
                "ru",
                "Ваш домен перенесён",
                ["Домен {{ domain.name }} перенесён к нам. Он действует до {{ domain.expires_at }}."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("{{ domain.name }} դոմենը տեղափոխվել է | {{ site.name }}", new SeededEmailCopy(
                "hy",
                "Ձեր դոմենը տեղափոխվել է",
                ["{{ domain.name }} դոմենը տեղափոխվել է մեզ մոտ։ Այն գրանցված է մինչև {{ domain.expires_at }}։"],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("{{ domain.name }} wurde transferiert | {{ site.name }}", new SeededEmailCopy(
                "de",
                "Ihre Domain wurde transferiert",
                ["{{ domain.name }} wurde zu uns transferiert. Sie ist bis zum {{ domain.expires_at }} registriert."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("{{ domain.name }} ha sido transferido | {{ site.name }}", new SeededEmailCopy(
                "es",
                "Su dominio ha sido transferido",
                ["{{ domain.name }} ha sido transferido a nosotros. Está registrado hasta el {{ domain.expires_at }}."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
        ]);
}
