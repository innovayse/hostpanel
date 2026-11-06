namespace Innovayse.Hostpanel.Application.Billing.Constants;

/// <summary>
/// The field keys of the bank transfer integration. The admin integrations form saves them under the
/// <see cref="BuiltInPaymentModules.BankTransferIntegrationSlug"/> slug, and the catalogue and the reader of the bank
/// details both use these, so a key is written once.
/// </summary>
public static class BankTransferFields
{
    /// <summary>The name of the account holder.</summary>
    public const string AccountName = "account_name";

    /// <summary>The IBAN the payer sends the money to.</summary>
    public const string Iban = "iban";

    /// <summary>The SWIFT/BIC code of the bank, for payers sending from abroad.</summary>
    public const string Swift = "swift";

    /// <summary>The name of the bank.</summary>
    public const string BankName = "bank_name";

    /// <summary>Free text the operator wants every payer to read.</summary>
    public const string Instructions = "instructions";

    /// <summary>The number of days an unconfirmed transfer may wait before it lapses and the invoice is open for payment again.</summary>
    public const string ExpiryDays = "expiry_days";
}
