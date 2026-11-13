using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Notifications;

namespace Innovayse.Hostpanel.Application.Notifications.Seeding;

/// <summary>
/// Seeds the two templates the billing engine sends, in every supported language, the first time
/// each is needed (the pattern of <see cref="EmailVerificationTemplateSeeder"/>): a migration-seeded
/// row would freeze the copy, while a runtime seed lets an operator edit it and never overwrites the edit.
/// </summary>
public static class RenewalTemplatesSeeder
{
    /// <summary>Slug of the client renewal reminder.</summary>
    public const string ReminderSlug = "invoice-renewal-reminder";

    /// <summary>Slug of the staff alert about a domain renewal that needs a person.</summary>
    public const string DomainFailedAdminSlug = "domain-renewal-failed-admin";

    /// <summary>Adds whichever language version of the two templates is missing.</summary>
    /// <param name="context">Data context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once every version exists.</returns>
    public static Task EnsureSeededAsync(IHostpanelDbContext context, CancellationToken ct)
    {
        return EmailTemplateSeeding.AddMissingAsync(
            context,
            [.. Reminder(), .. DomainFailedAdmin()],
            ct);
    }

    /// <summary>The client renewal reminder, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> Reminder() => EmailTemplateSeeding.Branded(
        ReminderSlug,
        "Sent before a renewal invoice is due, on each day listed in billing.renewal.reminder_days.",
        [
            new SeededEmailVersion("Invoice #{{ invoice.id }} is due on {{ invoice.due_date }} | {{ site.name }}", new SeededEmailCopy(
                "en",
                "Your renewal is coming up",
                ["Invoice #{{ invoice.id }} for {{ invoice.total }} {{ invoice.currency }} is due on {{ invoice.due_date }}. Pay it to keep your services running without interruption."],
                "{{ invoice.url }}",
                "View invoice",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Счёт №{{ invoice.id }} нужно оплатить до {{ invoice.due_date }} | {{ site.name }}", new SeededEmailCopy(
                "ru",
                "Скоро продление",
                ["Счёт №{{ invoice.id }} на {{ invoice.total }} {{ invoice.currency }} нужно оплатить до {{ invoice.due_date }}. Оплатите его, чтобы ваши услуги работали без перерыва."],
                "{{ invoice.url }}",
                "Открыть счёт",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("№{{ invoice.id }} հաշիվը պետք է վճարել մինչև {{ invoice.due_date }} | {{ site.name }}", new SeededEmailCopy(
                "hy",
                "Շուտով երկարաձգում",
                ["№{{ invoice.id }} հաշիվը՝ {{ invoice.total }} {{ invoice.currency }}, պետք է վճարել մինչև {{ invoice.due_date }}։ Վճարեք այն, որպեսզի ձեր ծառայություններն աշխատեն առանց ընդհատման։"],
                "{{ invoice.url }}",
                "Դիտել հաշիվը",
                Note: null,
                EchoButtonUrl: false)),
        ]);

    /// <summary>
    /// The staff alert about a domain renewal that needs a person, one version per language.
    /// invoice.id is 0 when no invoice was raised (an unpriceable domain), so it is shown only when real.
    /// </summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> DomainFailedAdmin() => EmailTemplateSeeding.Branded(
        DomainFailedAdminSlug,
        "Sent to staff when a paid or due domain renewal could not be completed automatically.",
        [
            new SeededEmailVersion("Domain renewal needs attention: {{ domain.name }}", new SeededEmailCopy(
                "en",
                "Domain renewal needs attention",
                ["{{ domain.name }} (client #{{ client.id }}{% if invoice.id != 0 %}, invoice #{{ invoice.id }}{% endif %}): {{ reason }}"],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Продление домена требует внимания: {{ domain.name }}", new SeededEmailCopy(
                "ru",
                "Продление домена требует внимания",
                ["{{ domain.name }} (клиент №{{ client.id }}{% if invoice.id != 0 %}, счёт №{{ invoice.id }}{% endif %}): {{ reason }}"],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Դոմենի երկարաձգումը ուշադրություն է պահանջում՝ {{ domain.name }}", new SeededEmailCopy(
                "hy",
                "Դոմենի երկարաձգումը ուշադրություն է պահանջում",
                ["{{ domain.name }} (հաճախորդ №{{ client.id }}{% if invoice.id != 0 %}, հաշիվ №{{ invoice.id }}{% endif %}): {{ reason }}"],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
        ]);
}
