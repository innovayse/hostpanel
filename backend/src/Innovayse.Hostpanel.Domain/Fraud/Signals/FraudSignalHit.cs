using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Domain.Fraud.Signals;

/// <summary>A signal that fired.</summary>
/// <param name="Reason">Why the request looks risky.</param>
/// <param name="Weight">How many points the signal adds to the score.</param>
public sealed record FraudSignalHit(FraudReasonCode Reason, int Weight);
