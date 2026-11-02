namespace Innovayse.Hostpanel.Domain.Products.Enums;

/// <summary>How often a product bills.</summary>
public enum BillingCycle
{
    /// <summary>Every month.</summary>
    Monthly = 0,

    /// <summary>Every year.</summary>
    Annual = 1,

    /// <summary>Every three months.</summary>
    Quarterly = 2,

    /// <summary>Every six months.</summary>
    SemiAnnual = 3,

    /// <summary>Every two years.</summary>
    Biennial = 4,
}
