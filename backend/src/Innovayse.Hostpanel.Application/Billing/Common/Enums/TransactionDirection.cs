namespace Innovayse.Hostpanel.Application.Billing.Common.Enums;

/// <summary>Which side of the ledger a transaction list shows. Travels on the wire by name.</summary>
public enum TransactionDirection
{
    /// <summary>Money received: the entries with an amount in.</summary>
    In = 0,

    /// <summary>Money sent: the entries with an amount out.</summary>
    Out = 1,
}
