namespace Innovayse.Hostpanel.Domain.Billing;

/// <summary>
/// The payment-method labels recorded on invoices and ledger transactions. They are persisted
/// display values, so they cannot become enum names without changing stored data.
/// </summary>
public static class PaymentMethodNames
{
    /// <summary>A card payment, recorded for every card-gateway payment and when an invoice names no method.</summary>
    public const string Card = "Card";

    /// <summary>A payment an administrator recorded by hand.</summary>
    public const string Manual = "Manual";

    /// <summary>A refund credited to the client's balance rather than paid out.</summary>
    public const string CreditBalance = "Credit Balance";

    /// <summary>A manual bank transfer.</summary>
    public const string BankTransfer = "Bank Transfer";

    /// <summary>The reference an invoice that owed nothing is marked paid with; no money moved.</summary>
    public const string NoCharge = "No Charge";

    /// <summary>The fragment that marks a payment method as a direct debit, matched case-insensitively.</summary>
    public const string DirectDebitMarker = "direct";
}
