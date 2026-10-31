namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// The tags the card settlement puts on its two reads. They are part of the contract between the
/// settlement and the tests that hold a request between those reads, so each is defined once here
/// and used by both.
/// </summary>
public static class SettlementQueryTags
{
    /// <summary>Tag of the settlement's load of the invoice with its payments.</summary>
    public const string InvoiceLoad = "settle-invoice-load";

    /// <summary>Tag of the committed read of the ledger for a gateway transaction.</summary>
    public const string LedgerRead = "settle-ledger-read";
}
