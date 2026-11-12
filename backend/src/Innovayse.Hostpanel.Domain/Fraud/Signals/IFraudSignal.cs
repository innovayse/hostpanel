namespace Innovayse.Hostpanel.Domain.Fraud.Signals;

/// <summary>One thing that makes a request look risky. A signal reads the facts and answers a hit or nothing; it does no I/O.</summary>
public interface IFraudSignal
{
    /// <summary>Looks at the facts of a request.</summary>
    /// <param name="context">The facts.</param>
    /// <returns>The hit with its reason and weight, or <see langword="null"/> when the signal does not fire.</returns>
    public FraudSignalHit? Evaluate(FraudSignalContext context);
}
