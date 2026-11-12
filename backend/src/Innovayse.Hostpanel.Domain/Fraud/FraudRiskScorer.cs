using Innovayse.Hostpanel.Domain.Fraud.Signals;

namespace Innovayse.Hostpanel.Domain.Fraud;

/// <summary>Runs the signals over the facts of one request and adds up what fired.</summary>
public static class FraudRiskScorer
{
    /// <summary>Scores a request with every signal the product knows.</summary>
    /// <param name="context">The facts of the request.</param>
    /// <returns>The score and the reasons.</returns>
    public static FraudAssessment Assess(FraudSignalContext context)
    {
        return Assess(context, FraudSignals.All);
    }

    /// <summary>Scores a request with the given signals.</summary>
    /// <param name="context">The facts of the request.</param>
    /// <param name="signals">The signals to run.</param>
    /// <returns>The score and the reasons.</returns>
    public static FraudAssessment Assess(FraudSignalContext context, IEnumerable<IFraudSignal> signals)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(signals);

        var hits = signals.Select(s => s.Evaluate(context)).OfType<FraudSignalHit>().ToList();
        return FraudAssessment.FromHits(hits);
    }
}
