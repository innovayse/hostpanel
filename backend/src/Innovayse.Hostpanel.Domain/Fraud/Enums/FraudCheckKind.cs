namespace Innovayse.Hostpanel.Domain.Fraud.Enums;

/// <summary>What was checked for fraud.</summary>
public enum FraudCheckKind
{
    /// <summary>A new account was being registered.</summary>
    Registration = 1,

    /// <summary>An order was being placed.</summary>
    Order = 2,
}
