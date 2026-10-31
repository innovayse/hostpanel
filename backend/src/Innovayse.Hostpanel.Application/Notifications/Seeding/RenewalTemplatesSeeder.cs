using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Notifications;

namespace Innovayse.Hostpanel.Application.Notifications.Seeding;

/// <summary>
/// Seeds the templates the billing engine sends, in every supported language, the first time
/// each is needed (the pattern of <see cref="EmailVerificationTemplateSeeder"/>): a migration-seeded
/// row would freeze the copy, while a runtime seed lets an operator edit it and never overwrites the edit.
/// </summary>
public static class RenewalTemplatesSeeder
{
    /// <summary>Slug of the client renewal reminder.</summary>
    public const string ReminderSlug = "invoice-renewal-reminder";

    /// <summary>Slug of the staff alert about a domain renewal that needs a person.</summary>
    public const string DomainFailedAdminSlug = "domain-renewal-failed-admin";

    /// <summary>Slug of the client mail that asks the cardholder to authenticate an automatic renewal charge (3-D Secure).</summary>
    public const string ChargeAuthenticationSlug = "invoice-renewal-charge-authentication";

    /// <summary>Slug of the client mail that says the saved card was declined for an automatic renewal charge.</summary>
    public const string ChargeDeclinedSlug = "invoice-renewal-charge-declined";

    /// <summary>Adds whichever language version of the templates is missing.</summary>
    /// <param name="context">Data context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once every version exists.</returns>
    public static Task EnsureSeededAsync(IHostpanelDbContext context, CancellationToken ct)
    {
        return EmailTemplateSeeding.AddMissingAsync(
            context,
            [.. Reminder(), .. DomainFailedAdmin(), .. ChargeAuthentication(), .. ChargeDeclined()],
            ct);
    }

    /// <summary>The client renewal reminder, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> Reminder() => EmailTemplateSeeding.Branded(
        ReminderSlug,
        "Sent before a renewal invoice is due, on each day listed in billing.renewal.reminder_days.",
        [
            new SeededEmailVersion("Invoice #{{ invoice.id }} is due on {{ invoice.due_date }} — {{ site.name }}", new SeededEmailCopy(
                "en",
                "Your renewal is coming up",
                ["Invoice #{{ invoice.id }} for {{ invoice.total }} {{ invoice.currency }} is due on {{ invoice.due_date }}. Pay it to keep your services running without interruption."],
                "{{ invoice.url }}",
                "View invoice",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Счёт №{{ invoice.id }} нужно оплатить до {{ invoice.due_date }} — {{ site.name }}", new SeededEmailCopy(
                "ru",
                "Скоро продление",
                ["Счёт №{{ invoice.id }} на {{ invoice.total }} {{ invoice.currency }} нужно оплатить до {{ invoice.due_date }}. Оплатите его, чтобы ваши услуги работали без перерыва."],
                "{{ invoice.url }}",
                "Открыть счёт",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("№{{ invoice.id }} հաշիվը պետք է վճարել մինչև {{ invoice.due_date }} — {{ site.name }}", new SeededEmailCopy(
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

    /// <summary>The mail that asks the cardholder to authenticate an automatic renewal charge, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> ChargeAuthentication() => EmailTemplateSeeding.Branded(
        ChargeAuthenticationSlug,
        "Sent when the bank asks the cardholder to confirm an automatic renewal charge (3-D Secure), so the invoice stays unpaid until they pay it.",
        [
            new SeededEmailVersion("Confirm the payment of invoice #{{ invoice.id }} — {{ site.name }}", new SeededEmailCopy(
                "en",
                "We could not charge your card automatically",
                ["Your bank needs you to confirm the payment of invoice #{{ invoice.id }} for {{ invoice.total }} {{ invoice.currency }}, due on {{ invoice.due_date }}. Open the invoice and pay with your card to keep your services running without interruption."],
                "{{ invoice.url }}",
                "Pay invoice",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Подтвердите оплату счёта №{{ invoice.id }} — {{ site.name }}", new SeededEmailCopy(
                "ru",
                "Не удалось списать оплату с карты автоматически",
                ["Банк просит вас подтвердить оплату счёта №{{ invoice.id }} на {{ invoice.total }} {{ invoice.currency }}, срок оплаты {{ invoice.due_date }}. Откройте счёт и оплатите картой, чтобы ваши услуги работали без перерыва."],
                "{{ invoice.url }}",
                "Оплатить счёт",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Հաստատեք №{{ invoice.id }} հաշվի վճարումը — {{ site.name }}", new SeededEmailCopy(
                "hy",
                "Չհաջողվեց քարտից ավտոմատ գանձել",
                ["Ձեր բանկը խնդրում է հաստատել №{{ invoice.id }} հաշվի՝ {{ invoice.total }} {{ invoice.currency }}, վճարումը (վճարման ժամկետը՝ {{ invoice.due_date }})։ Բացեք հաշիվը և վճարեք քարտով, որպեսզի ձեր ծառայություններն աշխատեն առանց ընդհատման։"],
                "{{ invoice.url }}",
                "Վճարել հաշիվը",
                Note: null,
                EchoButtonUrl: false)),
        ]);

    /// <summary>The mail that says the saved card was declined for an automatic renewal charge, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> ChargeDeclined() => EmailTemplateSeeding.Branded(
        ChargeDeclinedSlug,
        "Sent when the saved card was declined for an automatic renewal charge, so the invoice stays unpaid until the client pays it.",
        [
            new SeededEmailVersion("Your card could not be charged for invoice #{{ invoice.id }} — {{ site.name }}", new SeededEmailCopy(
                "en",
                "Your card could not be charged",
                ["We tried to charge your saved card for invoice #{{ invoice.id }} ({{ invoice.total }} {{ invoice.currency }}, due on {{ invoice.due_date }}) and the payment was declined. Pay the invoice with another card or update your payment method to keep your services running without interruption."],
                "{{ invoice.url }}",
                "Pay invoice",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Не удалось списать оплату счёта №{{ invoice.id }} с карты — {{ site.name }}", new SeededEmailCopy(
                "ru",
                "Не удалось списать оплату с карты",
                ["Мы попытались списать оплату счёта №{{ invoice.id }} ({{ invoice.total }} {{ invoice.currency }}, срок оплаты {{ invoice.due_date }}) с вашей сохранённой карты, но платёж был отклонён. Оплатите счёт другой картой или обновите способ оплаты, чтобы ваши услуги работали без перерыва."],
                "{{ invoice.url }}",
                "Оплатить счёт",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("№{{ invoice.id }} հաշվի վճարումը քարտից չգանձվեց — {{ site.name }}", new SeededEmailCopy(
                "hy",
                "Չհաջողվեց քարտից գանձել",
                ["Մենք փորձեցինք №{{ invoice.id }} հաշվի վճարումը ({{ invoice.total }} {{ invoice.currency }}, վճարման ժամկետը՝ {{ invoice.due_date }}) գանձել ձեր պահպանված քարտից, սակայն վճարումը մերժվեց։ Վճարեք հաշիվը այլ քարտով կամ թարմացրեք վճարման եղանակը, որպեսզի ձեր ծառայություններն աշխատեն առանց ընդհատման։"],
                "{{ invoice.url }}",
                "Վճարել հաշիվը",
                Note: null,
                EchoButtonUrl: false)),
        ]);
}
