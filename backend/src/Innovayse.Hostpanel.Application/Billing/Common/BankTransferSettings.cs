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
public sealed record BankTransferSettings(
    bool IsEnabled,
    string? AccountName,
    string? Iban,
    string? Swift,
    string? BankName,
    string? Instructions)
{
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
