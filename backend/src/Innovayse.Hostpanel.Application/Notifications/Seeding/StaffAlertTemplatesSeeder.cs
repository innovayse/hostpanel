using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Notifications;

namespace Innovayse.Hostpanel.Application.Notifications.Seeding;

/// <summary>
/// Seeds the staff alerts of the billing and provisioning jobs, in every supported language, the first
/// time each is needed (the pattern of <see cref="RenewalTemplatesSeeder"/>): a runtime seed lets an
/// operator edit the copy and never overwrites the edit.
/// </summary>
public static class StaffAlertTemplatesSeeder
{
    /// <summary>Slug of the staff alert about an automatic termination that could not remove the hosting account.</summary>
    public const string ServiceTerminationFailedSlug = "service-termination-failed-admin";

    /// <summary>Slug of the staff alert about a gateway payment that stays pending for an unusual time.</summary>
    public const string GatewayPaymentStalledSlug = "gateway-payment-stalled-admin";

    /// <summary>Slug of the staff alert about a gateway payment the invoice could not record, kept on the client's balance or held.</summary>
    public const string GatewayPaymentCreditedSlug = "gateway-payment-credited-admin";

    /// <summary>Slug of the staff alert about a gateway payment reported as paid with no reference to record it under.</summary>
    public const string GatewayPaymentWithoutReferenceSlug = "gateway-payment-without-reference-admin";

    /// <summary>Adds whichever language version of the staff alerts is missing.</summary>
    /// <param name="context">Data context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once every version exists.</returns>
    public static Task EnsureSeededAsync(IHostpanelDbContext context, CancellationToken ct)
    {
        return EmailTemplateSeeding.AddMissingAsync(
            context,
            [.. ServiceTerminationFailed(), .. GatewayPaymentStalled(), .. GatewayPaymentCredited(), .. GatewayPaymentWithoutReference()],
            ct);
    }

    /// <summary>The staff alert about a termination that could not remove the hosting account, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> ServiceTerminationFailed() => EmailTemplateSeeding.Branded(
        ServiceTerminationFailedSlug,
        "Sent to staff when an automatic termination could not remove the hosting account; the service stays as it was and the next run retries it.",
        [
            new SeededEmailVersion("Service termination failed: service #{{ service.id }}", new SeededEmailCopy(
                "en",
                "Service termination failed",
                ["Service #{{ service.id }} could not be terminated automatically, so its hosting account was not removed. The next run retries it. Reason: {{ reason }}"],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Не удалось завершить услугу №{{ service.id }}", new SeededEmailCopy(
                "ru",
                "Не удалось завершить услугу",
                ["Услугу №{{ service.id }} не удалось завершить автоматически, поэтому её аккаунт хостинга не удалён. Следующий запуск повторит попытку. Причина: {{ reason }}"],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Ծառայության դադարեցումը ձախողվեց՝ №{{ service.id }}", new SeededEmailCopy(
                "hy",
                "Ծառայության դադարեցումը ձախողվեց",
                ["№{{ service.id }} ծառայությունը չհաջողվեց ավտոմատ դադարեցնել, ուստի նրա հոստինգ հաշիվը չհեռացվեց։ Հաջորդ գործարկումը կփորձի կրկին։ Պատճառ՝ {{ reason }}"],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
        ]);

    /// <summary>The staff alert about a gateway payment pending for an unusual time, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> GatewayPaymentStalled() => EmailTemplateSeeding.Branded(
        GatewayPaymentStalledSlug,
        "Sent to staff once per payment session when a gateway payment is still pending after an unusual time.",
        [
            new SeededEmailVersion("Payment still pending: invoice #{{ invoice.id }}", new SeededEmailCopy(
                "en",
                "Payment still pending",
                ["The {{ module }} payment for invoice #{{ invoice.id }} (client #{{ client.id }}) has been pending for {{ pending_hours }} hours. The system keeps asking the gateway until it gives a final answer."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Платёж всё ещё в обработке: счёт №{{ invoice.id }}", new SeededEmailCopy(
                "ru",
                "Платёж всё ещё в обработке",
                ["Платёж {{ module }} по счёту №{{ invoice.id }} (клиент №{{ client.id }}) в обработке уже {{ pending_hours }} ч. Система продолжает опрашивать шлюз, пока он не даст окончательный ответ."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Վճարումը դեռ սպասման մեջ է՝ հաշիվ №{{ invoice.id }}", new SeededEmailCopy(
                "hy",
                "Վճարումը դեռ սպասման մեջ է",
                ["№{{ invoice.id }} հաշվի {{ module }} վճարումը (հաճախորդ №{{ client.id }}) սպասման մեջ է արդեն {{ pending_hours }} ժամ։ Համակարգը շարունակում է հարցում անել դարպասին, մինչև այն վերջնական պատասխան տա։"],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
        ]);

    /// <summary>
    /// The staff alert about a gateway payment the invoice could not record, one version per language.
    /// One template covers both outcomes: the money is on the client's balance, or it is held for an operator.
    /// </summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> GatewayPaymentCredited() => EmailTemplateSeeding.Branded(
        GatewayPaymentCreditedSlug,
        "Sent to staff once when a gateway payment took money its invoice could not record: the part above what was due, or all of it when the invoice was already settled or closed.",
        [
            new SeededEmailVersion("Gateway payment not recorded on invoice #{{ invoice.id }}", new SeededEmailCopy(
                "en",
                "Gateway payment not recorded on the invoice",
                ["The {{ module }} payment {{ reference }} for invoice #{{ invoice.id }} (client #{{ client.id }}) took {{ amount }} {{ currency }} that the invoice could not record. {% if credited %}The amount was added to the client's credit balance.{% else %}The amount could not be credited and is held for an operator to resolve.{% endif %}"],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Платёж через шлюз не записан по счёту №{{ invoice.id }}", new SeededEmailCopy(
                "ru",
                "Платёж через шлюз не записан по счёту",
                ["Платёж {{ module }} {{ reference }} по счёту №{{ invoice.id }} (клиент №{{ client.id }}) принёс {{ amount }} {{ currency }}, которые счёт не смог записать. {% if credited %}Сумма добавлена на кредитный баланс клиента.{% else %}Сумму не удалось зачислить, она удержана до решения оператора.{% endif %}"],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Դարպասի վճարումը չի գրանցվել №{{ invoice.id }} հաշվում", new SeededEmailCopy(
                "hy",
                "Դարպասի վճարումը չի գրանցվել հաշվում",
                ["№{{ invoice.id }} հաշվի {{ module }} {{ reference }} վճարումը (հաճախորդ №{{ client.id }}) ստացել է {{ amount }} {{ currency }}, որը հաշիվը չկարողացավ գրանցել։ {% if credited %}Գումարը ավելացվել է հաճախորդի վարկային մնացորդին։{% else %}Գումարը հնարավոր չեղավ ավելացնել մնացորդին, այն պահվում է օպերատորի որոշման համար։{% endif %}"],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
        ]);

    /// <summary>
    /// The staff alert about a gateway payment reported as paid with no reference to record it under, one
    /// version per language. The amount is printed only when the gateway reported one.
    /// </summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> GatewayPaymentWithoutReference() => EmailTemplateSeeding.Branded(
        GatewayPaymentWithoutReferenceSlug,
        "Sent to staff once per payment session when a gateway reports a payment as paid but no reference exists to record it under, so nothing was recorded or credited.",
        [
            new SeededEmailVersion("Paid gateway payment without a reference: invoice #{{ invoice.id }}", new SeededEmailCopy(
                "en",
                "Paid gateway payment without a reference",
                ["The {{ module }} gateway reports a payment{% if amount %} of {{ amount }} {{ currency }}{% endif %} as paid for invoice #{{ invoice.id }} (client #{{ client.id }}), but it carries no payment reference. Nothing was recorded or credited, because the payment could not be told apart from another. An operator has to find it at the gateway and record it by hand."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Оплата через шлюз без идентификатора платежа: счёт №{{ invoice.id }}", new SeededEmailCopy(
                "ru",
                "Оплата через шлюз без идентификатора платежа",
                ["Шлюз {{ module }} сообщает об оплате{% if amount %} на {{ amount }} {{ currency }}{% endif %} по счёту №{{ invoice.id }} (клиент №{{ client.id }}), но шлюз не передал идентификатор платежа. Ничего не записано и не зачислено, потому что платёж нельзя отличить от другого. Оператору нужно найти его в шлюзе и записать вручную."],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Վճարված դարպասի վճարում առանց վճարման նույնացուցիչի՝ հաշիվ №{{ invoice.id }}", new SeededEmailCopy(
                "hy",
                "Վճարված դարպասի վճարում առանց վճարման նույնացուցիչի",
                ["{{ module }} դարպասը №{{ invoice.id }} հաշվի (հաճախորդ №{{ client.id }}) համար հայտնում է վճարման մասին{% if amount %}՝ {{ amount }} {{ currency }}{% endif %}, բայց վճարման նույնացուցիչ չի փոխանցվել։ Ոչինչ չի գրանցվել և չի ավելացվել մնացորդին, քանի որ վճարումը հնարավոր չէ տարբերել մյուսից։ Օպերատորը պետք է գտնի այն դարպասում և գրանցի ձեռքով։"],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
        ]);
}
