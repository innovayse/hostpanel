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
        ]);
}
