using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Domain.Fraud.Signals;

/// <summary>Fires when both the billing country and the country of the IP address are known and differ.</summary>
public sealed class CountryMismatchSignal : IFraudSignal
{
    /// <summary>Points a mismatch adds.</summary>
    public const int Weight = 30;

    /// <inheritdoc/>
    public FraudSignalHit? Evaluate(FraudSignalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.BillingCountry) || string.IsNullOrWhiteSpace(context.IpCountry))
        {
            return null;
        }

        return string.Equals(context.BillingCountry.Trim(), context.IpCountry.Trim(), StringComparison.OrdinalIgnoreCase)
            ? null
            : new FraudSignalHit(FraudReasonCode.CountryMismatch, Weight);
    }
}
