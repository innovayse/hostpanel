using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Notifications;

namespace Innovayse.Hostpanel.Application.Notifications.Seeding;

/// <summary>
/// Seeds the four emails of the manual bank transfer flow (the bank details for the client, the notice for staff, the
/// confirmation and the rejection), in every supported language, the first time each is needed (the pattern of
/// <see cref="CardChargeTemplatesSeeder"/>): a runtime seed lets an operator edit the copy and never overwrites the edit.
/// </summary>
public static class BankTransferTemplatesSeeder
{
    /// <summary>Slug of the client email with the bank details and the reference to write on the transfer.</summary>
    public const string DetailsSlug = "bank-transfer-details";

    /// <summary>Slug of the staff notice that a client chose to pay by bank transfer.</summary>
    public const string RequestedAdminSlug = "bank-transfer-requested-admin";

    /// <summary>Slug of the client email that the transfer arrived and the invoice is paid.</summary>
    public const string ConfirmedSlug = "bank-transfer-confirmed";

    /// <summary>Slug of the client email that the transfer was not accepted and the invoice is open again.</summary>
    public const string RejectedSlug = "bank-transfer-rejected";

    /// <summary>Adds whichever language version of the four templates is missing.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once every version exists.</returns>
    public static Task EnsureSeededAsync(IHostpanelDbContext context, CancellationToken ct)
    {
        return EmailTemplateSeeding.AddMissingAsync(
            context,
            [.. Details(), .. RequestedAdmin(), .. Confirmed(), .. Rejected()],
            ct);
    }

    /// <summary>The client email with the bank details, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> Details() => EmailTemplateSeeding.Branded(
        DetailsSlug,
        "Sent to the client when they choose to pay an invoice by bank transfer: the bank details and the reference to write on the transfer.",
        [
            new SeededEmailVersion("Bank transfer details for invoice #{{ invoice.id }} | {{ site.name }}", new SeededEmailCopy(
                "en",
                "Pay invoice #{{ invoice.id }} by bank transfer",
                [
                    "Please transfer {{ invoice.total }} {{ invoice.currency }} and write <strong>{{ invoice.reference }}</strong> as the payment reference, so we can match your payment. The invoice is marked paid once we confirm the money arrived.",
                    "{% if bank.account_name != '' %}Account holder: {{ bank.account_name }}<br>{% endif %}{% if bank.bank_name != '' %}Bank: {{ bank.bank_name }}<br>{% endif %}{% if bank.iban != '' %}IBAN: {{ bank.iban }}<br>{% endif %}{% if bank.swift != '' %}SWIFT/BIC: {{ bank.swift }}<br>{% endif %}{% if bank.instructions != '' %}{{ bank.instructions }}{% endif %}",
                ],
                "{{ invoice.url }}",
                "View invoice",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Реквизиты для оплаты счёта №{{ invoice.id }} | {{ site.name }}", new SeededEmailCopy(
                "ru",
                "Оплата счёта №{{ invoice.id }} банковским переводом",
                [
                    "Переведите {{ invoice.total }} {{ invoice.currency }} и укажите в назначении платежа <strong>{{ invoice.reference }}</strong>, чтобы мы могли сопоставить ваш платёж. Счёт будет отмечен оплаченным, когда мы подтвердим поступление денег.",
                    "{% if bank.account_name != '' %}Получатель: {{ bank.account_name }}<br>{% endif %}{% if bank.bank_name != '' %}Банк: {{ bank.bank_name }}<br>{% endif %}{% if bank.iban != '' %}IBAN: {{ bank.iban }}<br>{% endif %}{% if bank.swift != '' %}SWIFT/BIC: {{ bank.swift }}<br>{% endif %}{% if bank.instructions != '' %}{{ bank.instructions }}{% endif %}",
                ],
                "{{ invoice.url }}",
                "Открыть счёт",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("№{{ invoice.id }} հաշվի բանկային փոխանցման տվյալները | {{ site.name }}", new SeededEmailCopy(
                "hy",
                "Վճարեք №{{ invoice.id }} հաշիվը բանկային փոխանցմամբ",
                [
                    "Փոխանցեք {{ invoice.total }} {{ invoice.currency }} և որպես վճարման նշում գրեք <strong>{{ invoice.reference }}</strong>, որպեսզի կարողանանք համապատասխանեցնել ձեր վճարումը։ Հաշիվը կնշվի որպես վճարված, երբ հաստատենք գումարի ստացումը։",
                    "{% if bank.account_name != '' %}Ստացող՝ {{ bank.account_name }}<br>{% endif %}{% if bank.bank_name != '' %}Բանկ՝ {{ bank.bank_name }}<br>{% endif %}{% if bank.iban != '' %}IBAN՝ {{ bank.iban }}<br>{% endif %}{% if bank.swift != '' %}SWIFT/BIC՝ {{ bank.swift }}<br>{% endif %}{% if bank.instructions != '' %}{{ bank.instructions }}{% endif %}",
                ],
                "{{ invoice.url }}",
                "Դիտել հաշիվը",
                Note: null,
                EchoButtonUrl: false)),
        ]);

    /// <summary>The staff notice that a bank transfer waits to be confirmed, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> RequestedAdmin() => EmailTemplateSeeding.Branded(
        RequestedAdminSlug,
        "Sent to staff when a client chooses to pay an invoice by bank transfer; the transfer waits in Pending bank transfers.",
        [
            new SeededEmailVersion("Bank transfer waiting: invoice #{{ invoice.id }}", new SeededEmailCopy(
                "en",
                "A bank transfer waits for confirmation",
                ["Client #{{ client.id }} chose to pay invoice #{{ invoice.id }} ({{ amount_due }} {{ currency }}) by bank transfer, reference {{ reference }}. When the money arrives, confirm it in Billing, Pending bank transfers; if it does not, reject it with a note."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Ожидается банковский перевод: счёт №{{ invoice.id }}", new SeededEmailCopy(
                "ru",
                "Банковский перевод ждёт подтверждения",
                ["Клиент №{{ client.id }} выбрал оплату счёта №{{ invoice.id }} ({{ amount_due }} {{ currency }}) банковским переводом, референс {{ reference }}. Когда деньги поступят, подтвердите перевод в разделе «Счета», «Ожидающие банковские переводы»; если нет, отклоните его с комментарием."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Սպասվում է բանկային փոխանցում՝ №{{ invoice.id }} հաշիվ", new SeededEmailCopy(
                "hy",
                "Բանկային փոխանցումը սպասում է հաստատման",
                ["Հաճախորդ №{{ client.id }}-ը ընտրել է №{{ invoice.id }} հաշվի ({{ amount_due }} {{ currency }}) վճարումը բանկային փոխանցմամբ, նշում՝ {{ reference }}։ Երբ գումարը ստացվի, հաստատեք այն «Հաշիվներ», «Սպասվող բանկային փոխանցումներ» բաժնում, իսկ եթե չստացվի՝ մերժեք մեկնաբանությամբ։"],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
        ]);

    /// <summary>The client email that the transfer arrived, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> Confirmed() => EmailTemplateSeeding.Branded(
        ConfirmedSlug,
        "Sent to the client when staff confirm that their bank transfer arrived and the invoice is paid.",
        [
            new SeededEmailVersion("Your bank transfer for invoice #{{ invoice.id }} was confirmed | {{ site.name }}", new SeededEmailCopy(
                "en",
                "Your bank transfer arrived",
                ["We received your bank transfer of {{ invoice.total }} {{ invoice.currency }} (reference {{ invoice.reference }}). Invoice #{{ invoice.id }} is paid. Thank you."],
                "{{ invoice.url }}",
                "View invoice",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Банковский перевод по счёту №{{ invoice.id }} подтверждён | {{ site.name }}", new SeededEmailCopy(
                "ru",
                "Ваш банковский перевод получен",
                ["Мы получили ваш банковский перевод на {{ invoice.total }} {{ invoice.currency }} (референс {{ invoice.reference }}). Счёт №{{ invoice.id }} оплачен. Спасибо."],
                "{{ invoice.url }}",
                "Открыть счёт",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("№{{ invoice.id }} հաշվի բանկային փոխանցումը հաստատվել է | {{ site.name }}", new SeededEmailCopy(
                "hy",
                "Ձեր բանկային փոխանցումը ստացվել է",
                ["Մենք ստացանք ձեր {{ invoice.total }} {{ invoice.currency }} բանկային փոխանցումը (նշում՝ {{ invoice.reference }})։ №{{ invoice.id }} հաշիվը վճարված է։ Շնորհակալություն։"],
                "{{ invoice.url }}",
                "Դիտել հաշիվը",
                Note: null,
                EchoButtonUrl: false)),
        ]);

    /// <summary>The client email that the transfer was not accepted, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> Rejected() => EmailTemplateSeeding.Branded(
        RejectedSlug,
        "Sent to the client when staff reject their bank transfer; the invoice is open again and the reason is in the mail.",
        [
            new SeededEmailVersion("Your bank transfer for invoice #{{ invoice.id }} was not accepted | {{ site.name }}", new SeededEmailCopy(
                "en",
                "We could not accept your bank transfer",
                [
                    "We could not match a bank transfer (reference {{ invoice.reference }}) to invoice #{{ invoice.id }}. Reason: {{ note }}",
                    "The invoice is open again, so you can pay it by bank transfer or another method.",
                ],
                "{{ invoice.url }}",
                "Pay invoice",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Банковский перевод по счёту №{{ invoice.id }} не принят | {{ site.name }}", new SeededEmailCopy(
                "ru",
                "Мы не смогли принять ваш банковский перевод",
                [
                    "Нам не удалось сопоставить банковский перевод (референс {{ invoice.reference }}) со счётом №{{ invoice.id }}. Причина: {{ note }}",
                    "Счёт снова открыт, поэтому вы можете оплатить его банковским переводом или другим способом.",
                ],
                "{{ invoice.url }}",
                "Оплатить счёт",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("№{{ invoice.id }} հաշվի բանկային փոխանցումը չի ընդունվել | {{ site.name }}", new SeededEmailCopy(
                "hy",
                "Չկարողացանք ընդունել ձեր բանկային փոխանցումը",
                [
                    "Չհաջողվեց համապատասխանեցնել բանկային փոխանցումը (նշում՝ {{ invoice.reference }}) №{{ invoice.id }} հաշվին։ Պատճառ՝ {{ note }}",
                    "Հաշիվը կրկին բաց է, ուստի կարող եք վճարել այն բանկային փոխանցմամբ կամ այլ եղանակով։",
                ],
                "{{ invoice.url }}",
                "Վճարել հաշիվը",
                Note: null,
                EchoButtonUrl: false)),
        ]);
}
