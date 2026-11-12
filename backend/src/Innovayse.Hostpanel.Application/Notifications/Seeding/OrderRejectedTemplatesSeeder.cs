using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Notifications;

namespace Innovayse.Hostpanel.Application.Notifications.Seeding;

/// <summary>
/// Seeds the email that tells a client their order was refused after a fraud review, in every supported language, the first time it
/// is needed (the pattern of <see cref="BankTransferTemplatesSeeder"/>): a runtime seed lets an operator edit the copy and never
/// overwrites the edit.
/// </summary>
public static class OrderRejectedTemplatesSeeder
{
    /// <summary>Slug of the client email that the order was not accepted, with the reason staff gave.</summary>
    public const string RejectedSlug = "order-rejected";

    /// <summary>Adds whichever language version of the template is missing.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once every version exists.</returns>
    public static Task EnsureSeededAsync(IHostpanelDbContext context, CancellationToken ct)
    {
        return EmailTemplateSeeding.AddMissingAsync(context, Rejected(), ct);
    }

    /// <summary>The client email that the order was refused, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> Rejected() => EmailTemplateSeeding.Branded(
        RejectedSlug,
        "Sent to the client when staff refuse an order after a fraud review; the reason staff gave is in the mail.",
        [
            new SeededEmailVersion("Your order {{ order_number }} was not accepted | {{ site.name }}", new SeededEmailCopy(
                "en",
                "We could not accept your order",
                [
                    "After a review we could not accept order {{ order_number }}. Reason: {{ reason }}",
                    "No services were set up for this order. If you think this is a mistake, please contact our support.",
                ],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Ваш заказ {{ order_number }} не принят | {{ site.name }}", new SeededEmailCopy(
                "ru",
                "Мы не смогли принять ваш заказ",
                [
                    "После проверки мы не смогли принять заказ {{ order_number }}. Причина: {{ reason }}",
                    "Услуги по этому заказу не подключались. Если вы считаете, что это ошибка, свяжитесь с нашей поддержкой.",
                ],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Ձեր {{ order_number }} պատվերը չի ընդունվել | {{ site.name }}", new SeededEmailCopy(
                "hy",
                "Չկարողացանք ընդունել ձեր պատվերը",
                [
                    "Ստուգումից հետո չկարողացանք ընդունել {{ order_number }} պատվերը։ Պատճառ՝ {{ reason }}",
                    "Այս պատվերի համար ծառայություններ չեն միացվել։ Եթե կարծում եք՝ սխալ է, դիմեք մեր աջակցությանը։",
                ],
                ButtonUrl: null,
                ButtonText: null,
                Note: null,
                EchoButtonUrl: false)),
        ]);
}
