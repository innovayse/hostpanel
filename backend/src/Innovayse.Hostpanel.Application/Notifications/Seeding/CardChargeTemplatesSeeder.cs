using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Notifications;

namespace Innovayse.Hostpanel.Application.Notifications.Seeding;

/// <summary>
/// Seeds the two client emails about a stored payment method charge that did not go through, in every
/// supported language, the first time each is needed (the pattern of <see cref="RenewalTemplatesSeeder"/>):
/// a runtime seed lets an operator edit the copy and never overwrites the edit.
/// </summary>
public static class CardChargeTemplatesSeeder
{
    /// <summary>Slug of the email sent when the gateway refused the charge.</summary>
    public const string DeclinedSlug = "card-charge-declined";

    /// <summary>Slug of the email sent when the payer must authenticate to complete the charge.</summary>
    public const string ActionRequiredSlug = "card-charge-action-required";

    /// <summary>Adds whichever language version of the two templates is missing.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once every version exists.</returns>
    public static Task EnsureSeededAsync(IHostpanelDbContext context, CancellationToken ct)
    {
        return EmailTemplateSeeding.AddMissingAsync(context, [.. Declined(), .. ActionRequired()], ct);
    }

    /// <summary>The "card charge declined" email, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> Declined() => EmailTemplateSeeding.Branded(
        DeclinedSlug,
        "Sent when a charge to the client's saved payment method was declined.",
        [
            new SeededEmailVersion("Your card was declined for invoice #{{ invoice.id }} — {{ site.name }}", new SeededEmailCopy(
                "en",
                "We could not charge your card",
                ["Your saved payment method was declined for invoice #{{ invoice.id }} ({{ invoice.total }} {{ invoice.currency }}). Nothing was charged. Please open the invoice to pay it another way."],
                "{{ invoice.url }}",
                "Pay invoice",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Карта отклонена по счёту №{{ invoice.id }} — {{ site.name }}", new SeededEmailCopy(
                "ru",
                "Не удалось списать оплату с карты",
                ["Ваш сохранённый способ оплаты был отклонён для счёта №{{ invoice.id }} ({{ invoice.total }} {{ invoice.currency }}). Деньги не списаны. Откройте счёт, чтобы оплатить его другим способом."],
                "{{ invoice.url }}",
                "Оплатить счёт",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Քարտը մերժվել է №{{ invoice.id }} հաշվի համար — {{ site.name }}", new SeededEmailCopy(
                "hy",
                "Չհաջողվեց գանձել քարտից",
                ["Ձեր պահված վճարման եղանակը մերժվել է №{{ invoice.id }} հաշվի համար ({{ invoice.total }} {{ invoice.currency }})։ Գումար չի գանձվել։ Բացեք հաշիվը՝ այն այլ եղանակով վճարելու համար։"],
                "{{ invoice.url }}",
                "Վճարել հաշիվը",
                Note: null,
                EchoButtonUrl: false)),
        ]);

    /// <summary>The "card charge needs your action" email, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> ActionRequired() => EmailTemplateSeeding.Branded(
        ActionRequiredSlug,
        "Sent when a charge to the client's saved payment method needs the client to confirm it with the bank.",
        [
            new SeededEmailVersion("Confirm the payment for invoice #{{ invoice.id }} — {{ site.name }}", new SeededEmailCopy(
                "en",
                "Your card needs your confirmation",
                ["Your bank asks you to confirm the payment of {{ invoice.total }} {{ invoice.currency }} for invoice #{{ invoice.id }}. The invoice stays unpaid until you do."],
                "{{ action_url }}",
                "Confirm payment",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Подтвердите оплату счёта №{{ invoice.id }} — {{ site.name }}", new SeededEmailCopy(
                "ru",
                "Нужно подтверждение платежа",
                ["Ваш банк просит подтвердить платёж на {{ invoice.total }} {{ invoice.currency }} по счёту №{{ invoice.id }}. Пока вы этого не сделаете, счёт остаётся неоплаченным."],
                "{{ action_url }}",
                "Подтвердить платёж",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Հաստատեք №{{ invoice.id }} հաշվի վճարումը — {{ site.name }}", new SeededEmailCopy(
                "hy",
                "Անհրաժեշտ է վճարման հաստատում",
                ["Ձեր բանկը խնդրում է հաստատել {{ invoice.total }} {{ invoice.currency }} վճարումը №{{ invoice.id }} հաշվի համար։ Մինչև հաստատումը հաշիվը մնում է չվճարված։"],
                "{{ action_url }}",
                "Հաստատել վճարումը",
                Note: null,
                EchoButtonUrl: false)),
        ]);
}
