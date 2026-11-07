using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Notifications;

namespace Innovayse.Hostpanel.Application.Notifications.Seeding;

/// <summary>
/// Seeds the email that tells a client a credit note was issued to them, in every supported language, the first time it is needed
/// (the pattern of <see cref="CreditBalanceTemplatesSeeder"/>): a runtime seed lets an operator edit the copy and never overwrites
/// the edit.
/// </summary>
public static class CreditNoteTemplatesSeeder
{
    /// <summary>Slug of the client email that announces an issued credit note.</summary>
    public const string IssuedSlug = "credit-note-issued";

    /// <summary>Adds whichever language version of the template is missing.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once every version exists.</returns>
    public static Task EnsureSeededAsync(IHostpanelDbContext context, CancellationToken ct)
    {
        return EmailTemplateSeeding.AddMissingAsync(context, [.. Issued()], ct);
    }

    /// <summary>The client email that announces a credit note, one version per language.</summary>
    /// <returns>The versions.</returns>
    private static IReadOnlyList<EmailTemplate> Issued() => EmailTemplateSeeding.Branded(
        IssuedSlug,
        "Sent to the client when a credit note is issued against one of their invoices. Carries the note number, the amount and the invoice it credits. A credit note is not an invoice credit: it reverses part of an invoice that was already paid.",
        [
            new SeededEmailVersion("Credit note {{ credit_note.number }} | {{ site.name }}", new SeededEmailCopy(
                "en",
                "A credit note was issued",
                [
                    "We issued credit note <strong>{{ credit_note.number }}</strong> for <strong>{{ credit_note.total }} {{ credit_note.currency }}</strong> against invoice #{{ credit_note.invoice_id }}.",
                    "You can view and download it from your client area.",
                ],
                "{{ credit_note.url }}",
                "View my credit notes",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Кредит-нота {{ credit_note.number }} | {{ site.name }}", new SeededEmailCopy(
                "ru",
                "Выпущена кредит-нота",
                [
                    "Мы выпустили кредит-ноту <strong>{{ credit_note.number }}</strong> на сумму <strong>{{ credit_note.total }} {{ credit_note.currency }}</strong> по счёту №{{ credit_note.invoice_id }}.",
                    "Вы можете посмотреть и скачать её в личном кабинете.",
                ],
                "{{ credit_note.url }}",
                "Открыть кредит-ноты",
                Note: null,
                EchoButtonUrl: false)),
            new SeededEmailVersion("Կրեդիտ-նոտա {{ credit_note.number }} | {{ site.name }}", new SeededEmailCopy(
                "hy",
                "Տրվել է կրեդիտ-նոտա",
                [
                    "Մենք տվել ենք <strong>{{ credit_note.number }}</strong> կրեդիտ-նոտան՝ <strong>{{ credit_note.total }} {{ credit_note.currency }}</strong> գումարով, #{{ credit_note.invoice_id }} հաշվի գծով։",
                    "Կարող եք դիտել և ներբեռնել այն ձեր հաճախորդի էջից։",
                ],
                "{{ credit_note.url }}",
                "Դիտել իմ կրեդիտ-նոտաները",
                Note: null,
                EchoButtonUrl: false)),
        ]);
}
