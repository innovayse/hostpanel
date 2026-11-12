using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Domain.Fraud.Signals;

/// <summary>Fires when the billing country, or the country of the IP address when it is known, is on the block list.</summary>
public sealed class BlockedCountrySignal : IFraudSignal
{
    /// <summary>Points a block list hit adds; enough to reach any reject threshold.</summary>
    public const int Weight = FraudAssessment.MaxScore;

    /// <inheritdoc/>
    public FraudSignalHit? Evaluate(FraudSignalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var countries = new[] { context.BillingCountry, context.IpCountry }
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.Trim().ToUpperInvariant())
            .ToList();

        var listed = context.BlockList.Any(
            entry => entry.Type == BlockListType.Country && countries.Contains(entry.Value, StringComparer.Ordinal));

        return listed ? new FraudSignalHit(FraudReasonCode.BlockedCountry, Weight) : null;
    }
}
