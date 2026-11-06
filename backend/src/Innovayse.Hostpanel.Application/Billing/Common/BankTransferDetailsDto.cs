namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// What a client needs to pay one invoice by manual bank transfer: the operator's bank details and the reference to write
/// on the transfer. The reference is built from the invoice number alone, so the internal client id is never shown.
/// </summary>
/// <param name="InvoiceId">The invoice the transfer pays.</param>
/// <param name="Reference">The reference to write on the transfer, so staff can match it to the invoice.</param>
/// <param name="AmountDue">What the transfer must be for, in <paramref name="Currency"/>.</param>
/// <param name="Currency">ISO 4217 code the invoice is billed in.</param>
/// <param name="AccountName">The account holder; null when the operator left it empty.</param>
/// <param name="Iban">The IBAN; null when the operator left it empty.</param>
/// <param name="Swift">The SWIFT/BIC code; null when the operator left it empty.</param>
/// <param name="BankName">The bank's name; null when the operator left it empty.</param>
/// <param name="Instructions">Free text from the operator; null when the operator left it empty.</param>
public sealed record BankTransferDetailsDto(
    int InvoiceId,
    string Reference,
    decimal AmountDue,
    string Currency,
    string? AccountName,
    string? Iban,
    string? Swift,
    string? BankName,
    string? Instructions);
