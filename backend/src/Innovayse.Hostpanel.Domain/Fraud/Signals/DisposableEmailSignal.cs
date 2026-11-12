using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Domain.Fraud.Signals;

/// <summary>Fires when the e-mail address is on a disposable mailbox service from <see cref="DisposableEmailDomains"/>.</summary>
public sealed class DisposableEmailSignal : IFraudSignal
{
    /// <summary>Points a disposable address adds: with the default thresholds it is held for review on its own.</summary>
    public const int Weight = 60;

    /// <inheritdoc/>
    public FraudSignalHit? Evaluate(FraudSignalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var domain = context.EmailDomain;
        return domain is not null && DisposableEmailDomains.Contains(domain)
            ? new FraudSignalHit(FraudReasonCode.DisposableEmail, Weight)
            : null;
    }
}
