namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// The credit note PDF labels in each language the panel is translated into (English, Russian, Armenian). A language with no
/// set prints English, so a client never gets a document with blank labels.
/// </summary>
public static class CreditNotePdfLabelSets
{
    /// <summary>Gets the English labels; also what an unknown language falls back to.</summary>
    public static CreditNotePdfLabels English { get; } = new(
        DocumentPdfLabels.English,
        "Credit note", "Issue date", "Invoice", "Status", "Reason", "Total credited",
        "Draft", "Issued", "Applied", "Voided", "DRAFT", "VOIDED");

    /// <summary>Gets the Russian labels.</summary>
    public static CreditNotePdfLabels Russian { get; } = new(
        new DocumentPdfLabels("Плательщик", "Описание", "Кол-во", "Цена", "Сумма", "Итого без налога", "Налог", "Кредит", "Итого", "Страница", "из", "ИНН"),
        "Кредит-нота", "Дата выпуска", "Счёт", "Статус", "Причина", "Итого к зачёту",
        "Черновик", "Выпущена", "Применена", "Аннулирована", "ЧЕРНОВИК", "АННУЛИРОВАНА");

    /// <summary>Gets the Armenian labels.</summary>
    public static CreditNotePdfLabels Armenian { get; } = new(
        new DocumentPdfLabels("Վճարող", "Նկարագրություն", "Քանակ", "Միավորի գին", "Գումար", "Ենթագումար", "Հարկ", "Կրեդիտ", "Ընդամենը", "Էջ", "/", "ՀՎՀՀ"),
        "Կրեդիտ-նոտա", "Տրման ամսաթիվ", "Հաշիվ", "Կարգավիճակ", "Պատճառ", "Կրեդիտի ընդհանուր գումար",
        "Սևագիր", "Տրված", "Կիրառված", "Չեղարկված", "ՍԵՎԱԳԻՐ", "ՉԵՂԱՐԿՎԱԾ");

    /// <summary>Picks the label set of the language of a client.</summary>
    /// <param name="language">The ISO 639-1 language code of the client, or null when none is set.</param>
    /// <returns>The set for <c>ru</c> or <c>hy</c>, otherwise English.</returns>
    public static CreditNotePdfLabels For(string? language) =>
        language?.Trim().ToLowerInvariant() switch
        {
            "ru" => Russian,
            "hy" => Armenian,
            _ => English,
        };
}
