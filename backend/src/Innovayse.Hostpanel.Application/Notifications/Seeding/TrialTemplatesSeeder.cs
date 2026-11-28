using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Notifications;

namespace Innovayse.Hostpanel.Application.Notifications.Seeding;

/// <summary>
/// Seeds the three free-trial emails (ending soon, converted to a paid service, ended) in every supported language the first time
/// they are needed (the pattern of <see cref="CreditNoteTemplatesSeeder"/>): a runtime seed lets an operator edit the copy and
/// never overwrites the edit. The German and Spanish copy is machine translated, pending native review.
/// </summary>
public static class TrialTemplatesSeeder
{
    /// <summary>Slug of the email that warns a trial is about to end.</summary>
    public const string EndingSlug = "trial-ending";

    /// <summary>Slug of the email that tells a trial became a paid service.</summary>
    public const string ConvertedSlug = "trial-converted";

    /// <summary>Slug of the email that tells a trial ended and the service was closed.</summary>
    public const string EndedSlug = "trial-ended";

    /// <summary>Adds whichever language version of each template is missing.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once every version exists.</returns>
    public static Task EnsureSeededAsync(IHostpanelDbContext context, CancellationToken ct)
    {
        return EmailTemplateSeeding.AddMissingAsync(context, [.. Ending(), .. Converted(), .. Ended()], ct);
    }

    /// <summary>The client email that warns a trial is about to end, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> Ending() => EmailTemplateSeeding.Branded(
        EndingSlug,
        "Sent to the client when a free trial is about to end, on each reminder day the product asked for. Carries the product, the end date and the days left.",
        [
            new SeededEmailVersion("Your free trial of {{ trial.product_name }} ends in {{ days_left }} days | {{ site.name }}", new SeededEmailCopy(
                "en",
                "Your free trial is ending",
                [
                    "Your free trial of <strong>{{ trial.product_name }}</strong> ends on {{ trial.end_date }}.",
                    "Upgrade to a paid plan to keep the service.",
                ],
                "{{ trial.service_url }}",
                "View the service",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Бесплатный период {{ trial.product_name }} закончится через {{ days_left }} дн. | {{ site.name }}", new SeededEmailCopy(
                "ru",
                "Ваш бесплатный период заканчивается",
                [
                    "Ваш бесплатный период для <strong>{{ trial.product_name }}</strong> заканчивается {{ trial.end_date }}.",
                    "Перейдите на платный тариф, чтобы сохранить услугу.",
                ],
                "{{ trial.service_url }}",
                "Открыть услугу",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("{{ trial.product_name }} անվճար փորձաշրջանը ավարտվում է {{ days_left }} օրից | {{ site.name }}", new SeededEmailCopy(
                "hy",
                "Ձեր անվճար փորձաշրջանը մոտենում է ավարտին",
                [
                    "<strong>{{ trial.product_name }}</strong> անվճար փորձաշրջանը ավարտվում է {{ trial.end_date }}-ին։",
                    "Անցեք վճարովի պլանի՝ ծառայությունը պահպանելու համար։",
                ],
                "{{ trial.service_url }}",
                "Դիտել ծառայությունը",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Ihre kostenlose Testphase für {{ trial.product_name }} endet in {{ days_left }} Tagen | {{ site.name }}", new SeededEmailCopy(
                "de",
                "Ihre kostenlose Testphase läuft aus",
                [
                    "Ihre kostenlose Testphase für <strong>{{ trial.product_name }}</strong> endet am {{ trial.end_date }}.",
                    "Wechseln Sie zu einem kostenpflichtigen Tarif, um den Dienst zu behalten.",
                ],
                "{{ trial.service_url }}",
                "Dienst ansehen",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Su prueba gratuita de {{ trial.product_name }} termina en {{ days_left }} días | {{ site.name }}", new SeededEmailCopy(
                "es",
                "Su prueba gratuita está por terminar",
                [
                    "Su prueba gratuita de <strong>{{ trial.product_name }}</strong> termina el {{ trial.end_date }}.",
                    "Pase a un plan de pago para conservar el servicio.",
                ],
                "{{ trial.service_url }}",
                "Ver el servicio",
                Note: null,
                EchoButtonUrl: false)),
        ]);

    /// <summary>The client email that tells a trial became a paid service, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> Converted() => EmailTemplateSeeding.Branded(
        ConvertedSlug,
        "Sent to the client when a free trial is converted into a paid service. Carries the product, the end date and the day the first invoice must be paid by.",
        [
            new SeededEmailVersion("Your trial of {{ trial.product_name }} is now a paid service | {{ site.name }}", new SeededEmailCopy(
                "en",
                "Your trial became a paid service",
                [
                    "Your free trial of <strong>{{ trial.product_name }}</strong> ended on {{ trial.end_date }} and we raised the invoice for its first period.",
                    "Pay it by {{ payment_due_date }} to keep the service; unpaid, it is closed on that day.",
                ],
                "{{ trial.service_url }}",
                "View the service",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Пробный период {{ trial.product_name }} стал платной услугой | {{ site.name }}", new SeededEmailCopy(
                "ru",
                "Ваш пробный период стал платной услугой",
                [
                    "Ваш бесплатный период для <strong>{{ trial.product_name }}</strong> закончился {{ trial.end_date }}, и мы выставили счёт за первый период.",
                    "Оплатите его до {{ payment_due_date }}, чтобы сохранить услугу; если счёт не оплачен, в этот день услуга будет закрыта.",
                ],
                "{{ trial.service_url }}",
                "Открыть услугу",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("{{ trial.product_name }} փորձաշրջանը դարձել է վճարովի ծառայություն | {{ site.name }}", new SeededEmailCopy(
                "hy",
                "Ձեր փորձաշրջանը դարձել է վճարովի ծառայություն",
                [
                    "<strong>{{ trial.product_name }}</strong> անվճար փորձաշրջանը ավարտվեց {{ trial.end_date }}-ին, և մենք դուրս գրեցինք առաջին ժամանակաշրջանի հաշիվը։",
                    "Վճարեք այն մինչև {{ payment_due_date }}՝ ծառայությունը պահպանելու համար. չվճարվելու դեպքում ծառայությունը փակվում է այդ օրը։",
                ],
                "{{ trial.service_url }}",
                "Դիտել ծառայությունը",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Ihre Testphase für {{ trial.product_name }} ist jetzt ein kostenpflichtiger Dienst | {{ site.name }}", new SeededEmailCopy(
                "de",
                "Ihre Testphase wurde zum kostenpflichtigen Dienst",
                [
                    "Ihre kostenlose Testphase für <strong>{{ trial.product_name }}</strong> endete am {{ trial.end_date }}, und wir haben die Rechnung für den ersten Zeitraum erstellt.",
                    "Bezahlen Sie sie bis zum {{ payment_due_date }}, um den Dienst zu behalten; unbezahlt wird er an diesem Tag geschlossen.",
                ],
                "{{ trial.service_url }}",
                "Dienst ansehen",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Su prueba de {{ trial.product_name }} es ahora un servicio de pago | {{ site.name }}", new SeededEmailCopy(
                "es",
                "Su prueba se convirtió en un servicio de pago",
                [
                    "Su prueba gratuita de <strong>{{ trial.product_name }}</strong> terminó el {{ trial.end_date }} y emitimos la factura de su primer periodo.",
                    "Pague la factura antes del {{ payment_due_date }} para conservar el servicio; si no se paga, se cierra ese día.",
                ],
                "{{ trial.service_url }}",
                "Ver el servicio",
                Note: null,
                EchoButtonUrl: false)),
        ]);

    /// <summary>The client email that tells a trial ended, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> Ended() => EmailTemplateSeeding.Branded(
        EndedSlug,
        "Sent to the client when a free trial is terminated instead of becoming a paid service. Carries the product and the end date.",
        [
            new SeededEmailVersion("Your free trial of {{ trial.product_name }} has ended | {{ site.name }}", new SeededEmailCopy(
                "en",
                "Your free trial has ended",
                [
                    "Your free trial of <strong>{{ trial.product_name }}</strong> ended on {{ trial.end_date }} and the service was closed.",
                    "You can order the product at any time from your client area.",
                ],
                "{{ trial.service_url }}",
                "View the service",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Бесплатный период {{ trial.product_name }} закончился | {{ site.name }}", new SeededEmailCopy(
                "ru",
                "Ваш бесплатный период закончился",
                [
                    "Ваш бесплатный период для <strong>{{ trial.product_name }}</strong> закончился {{ trial.end_date }}, услуга закрыта.",
                    "Вы можете заказать продукт в любое время в личном кабинете.",
                ],
                "{{ trial.service_url }}",
                "Открыть услугу",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("{{ trial.product_name }} անվճար փորձաշրջանը ավարտվել է | {{ site.name }}", new SeededEmailCopy(
                "hy",
                "Ձեր անվճար փորձաշրջանը ավարտվել է",
                [
                    "<strong>{{ trial.product_name }}</strong> անվճար փորձաշրջանը ավարտվեց {{ trial.end_date }}-ին, և ծառայությունը փակվեց։",
                    "Դուք կարող եք պատվիրել ապրանքը ցանկացած պահի ձեր հաճախորդի էջից։",
                ],
                "{{ trial.service_url }}",
                "Դիտել ծառայությունը",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Ihre kostenlose Testphase für {{ trial.product_name }} ist beendet | {{ site.name }}", new SeededEmailCopy(
                "de",
                "Ihre kostenlose Testphase ist beendet",
                [
                    "Ihre kostenlose Testphase für <strong>{{ trial.product_name }}</strong> endete am {{ trial.end_date }}, und der Dienst wurde geschlossen.",
                    "Sie können das Produkt jederzeit in Ihrem Kundenbereich bestellen.",
                ],
                "{{ trial.service_url }}",
                "Dienst ansehen",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Su prueba gratuita de {{ trial.product_name }} ha terminado | {{ site.name }}", new SeededEmailCopy(
                "es",
                "Su prueba gratuita ha terminado",
                [
                    "Su prueba gratuita de <strong>{{ trial.product_name }}</strong> terminó el {{ trial.end_date }} y el servicio se cerró.",
                    "Puede pedir el producto en cualquier momento desde su área de cliente.",
                ],
                "{{ trial.service_url }}",
                "Ver el servicio",
                Note: null,
                EchoButtonUrl: false)),
        ]);
}
