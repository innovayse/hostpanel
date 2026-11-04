namespace Innovayse.Hostpanel.Application.Billing.Common.Enums;

/// <summary>What became of the money of a card payment that could not be recorded as an invoice payment.</summary>
public enum CardReturnState
{
    /// <summary>The ledger holds no handed-back row: the money is not accounted for as returned.</summary>
    NotRecorded = 0,

    /// <summary>The money was refunded to the card or credited to the client's balance.</summary>
    Returned = 1,

    /// <summary>Neither could be done and an operator has to return the money; the invoice is still payable meanwhile.</summary>
    HeldForOperator = 2,
}
