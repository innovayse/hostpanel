namespace Innovayse.Hostpanel.Domain.Billing.Enums;

/// <summary>Which of a member state's VAT rates a <see cref="VatRate"/> row holds.</summary>
public enum VatRateKind
{
    /// <summary>The standard rate, the one charged on electronic services to consumers.</summary>
    Standard = 0,

    /// <summary>A reduced rate the member state applies to a narrower class of supplies; stored for reference only, never applied to an invoice automatically.</summary>
    Reduced = 1,
}
