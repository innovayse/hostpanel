using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Notifications;

namespace Innovayse.Hostpanel.Application.Notifications.Seeding;

/// <summary>
/// Seeds the email that tells a client their credit balance changed, in every supported language, the first time it is
/// needed (the pattern of <see cref="BankTransferTemplatesSeeder"/>): a runtime seed lets an operator edit the copy and
/// never overwrites the edit.
/// </summary>
public static class CreditBalanceTemplatesSeeder
{
    /// <summary>Slug of the client email with the signed change and the new credit balance.</summary>
    public const string ChangedSlug = "credit-balance-changed";

    /// <summary>Adds whichever language version of the template is missing.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once every version exists.</returns>
    public static Task EnsureSeededAsync(IHostpanelDbContext context, CancellationToken ct)
    {
        return EmailTemplateSeeding.AddMissingAsync(context, [.. Changed()], ct);
    }

    /// <summary>The client email with the change and the new balance, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> Changed() => EmailTemplateSeeding.Branded(
        ChangedSlug,
        "Sent to the client when their credit balance changes without an invoice they paid for it: a top-up, a refund to the balance, or an adjustment by staff. Carries the change and the new balance.",
        [
            new SeededEmailVersion("Your credit balance is now {{ credit.balance }} {{ credit.currency }} | {{ site.name }}", new SeededEmailCopy(
                "en",
                "Your credit balance changed",
                [
                    "Your credit balance changed by {{ credit.change }} {{ credit.currency }}. Your balance is now <strong>{{ credit.balance }} {{ credit.currency }}</strong>.",
                    "You can use it to pay your invoices.",
                ],
                "{{ credit.url }}",
                "View my credit",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Ваш баланс теперь {{ credit.balance }} {{ credit.currency }} | {{ site.name }}", new SeededEmailCopy(
                "ru",
                "Баланс вашего счёта изменился",
                [
                    "Ваш баланс изменился на {{ credit.change }} {{ credit.currency }}. Теперь на балансе <strong>{{ credit.balance }} {{ credit.currency }}</strong>.",
                    "Вы можете использовать его для оплаты счетов.",
                ],
                "{{ credit.url }}",
                "Открыть баланс",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Ձեր մնացորդը այժմ {{ credit.balance }} {{ credit.currency }} է | {{ site.name }}", new SeededEmailCopy(
                "hy",
                "Ձեր մնացորդը փոխվել է",
                [
                    "Ձեր մնացորդը փոխվել է {{ credit.change }} {{ credit.currency }}-ով։ Այժմ ձեր մնացորդը <strong>{{ credit.balance }} {{ credit.currency }}</strong> է։",
                    "Կարող եք այն օգտագործել հաշիվների վճարման համար։",
                ],
                "{{ credit.url }}",
                "Դիտել իմ մնացորդը",
                Note: null,
                EchoButtonUrl: false)),
        ]);
}
