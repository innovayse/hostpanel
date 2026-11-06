namespace Innovayse.Hostpanel.Application.Billing.Common.Enums;

/// <summary>Which way staff move a client's credit balance by hand. Travels on the wire by name.</summary>
public enum CreditAdjustment
{
    /// <summary>Credit is added to the balance.</summary>
    Add = 0,

    /// <summary>Credit is taken off the balance, never more than it holds.</summary>
    Deduct = 1,
}
