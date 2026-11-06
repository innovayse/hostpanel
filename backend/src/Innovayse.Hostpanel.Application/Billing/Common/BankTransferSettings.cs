using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// The operator's bank transfer integration as stored: whether it is switched on and the bank details the admin
/// integrations form saved. Read through the Admin module (<c>BankTransferSettingsExtensions</c>), never from its tables.
/// </summary>
/// <param name="IsEnabled">Whether the admin switched bank transfer on.</param>
/// <param name="AccountName">The account holder; null when empty.</param>
/// <param name="Iban">The IBAN; null when empty.</param>
/// <param name="Swift">The SWIFT/BIC code; null when empty.</param>
/// <param name="BankName">The bank's name; null when empty.</param>
/// <param name="Instructions">Free text for the payer; null when empty.</param>
/// <param name="ExpiryDays">Days an unconfirmed transfer may wait before it lapses; <see cref="DefaultExpiryDays"/> when unset or invalid.</param>
public sealed record BankTransferSettings(
    bool IsEnabled,
    string? AccountName,
    string? Iban,
    string? Swift,
    string? BankName,
    string? Instructions,
    int ExpiryDays = BankTransferSettings.DefaultExpiryDays)
{
    /// <summary>Days an unconfirmed transfer waits when the admin did not set a number.</summary>
    public const int DefaultExpiryDays = 14;

    /// <summary>
    /// Gets whether the payer is given anything to pay with: at least one of the account holder, IBAN, SWIFT, bank name or
    /// the instructions is set. Without it the transfer card would be empty, so the method is not offered.
    /// </summary>
    public bool HasPayerDetails =>
        AccountName is not null || Iban is not null || Swift is not null || BankName is not null || Instructions is not null;

    /// <summary>Joins the bank details with one invoice, giving what the client sees.</summary>
    /// <param name="invoice">The invoice the transfer pays.</param>
    /// <returns>The details, with the invoice's reference, amount still due and currency.</returns>
    public BankTransferDetailsDto ToDetails(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        return new BankTransferDetailsDto(
            invoice.Id,
            invoice.BankTransferReference,
            invoice.AmountDue,
            invoice.Currency,
            AccountName,
            Iban,
            Swift,
            BankName,
            Instructions);
    }
}
