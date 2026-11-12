using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Domain.Fraud.Signals;

/// <summary>Fires when this request would take the number of accounts seen from one IP address past the limit.</summary>
public sealed class SharedIpAccountsSignal : IFraudSignal
{
    /// <summary>Points several accounts on one address add.</summary>
    public const int Weight = 40;

    /// <inheritdoc/>
    public FraudSignalHit? Evaluate(FraudSignalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The others already seen plus this one would exceed the limit exactly when the others reach it.
        return string.IsNullOrWhiteSpace(context.IpAddress) || context.OtherAccountsOnIp < context.MaxAccountsPerIp
            ? null
            : new FraudSignalHit(FraudReasonCode.SharedIpAccounts, Weight);
    }
}
