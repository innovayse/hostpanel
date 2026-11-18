namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>How a new invoice for a client is taxed, as <c>ClientTaxRateResolver</c> decides it.</summary>
/// <param name="Rate">The tax rate in percent; 0 when none applies.</param>
/// <param name="ReverseChargeVatNumber">
/// The client's VAT number when the invoice is raised under the reverse charge (no tax, the client accounts for it), or
/// null when it is not.
/// </param>
public sealed record ClientTaxTreatment(decimal Rate, string? ReverseChargeVatNumber = null)
{
    /// <summary>Gets whether the invoice is raised under the reverse charge.</summary>
    public bool ReverseCharge => ReverseChargeVatNumber is not null;
}
