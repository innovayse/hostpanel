using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Domain.Fraud.Signals;

/// <summary>Fires when the e-mail address belongs to a domain on the block list.</summary>
public sealed class BlockedEmailDomainSignal : IFraudSignal
{
    /// <summary>Points a block list hit adds; enough to reach any reject threshold.</summary>
    public const int Weight = FraudAssessment.MaxScore;

    /// <inheritdoc/>
    public FraudSignalHit? Evaluate(FraudSignalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var domain = context.EmailDomain;
        if (domain is null)
        {
            return null;
        }

        var listed = context.BlockList.Any(
            entry => entry.Type == BlockListType.EmailDomain && EmailDomains.IsSameOrSubdomain(domain, entry.Value));

        return listed ? new FraudSignalHit(FraudReasonCode.BlockedEmailDomain, Weight) : null;
    }
}
