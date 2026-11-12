using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Domain.Fraud.Signals;

namespace Innovayse.Hostpanel.Domain.Fraud;

/// <summary>A risk score with the reasons it came from.</summary>
/// <param name="Score">The score, 0 (no risk seen) to <see cref="MaxScore"/>.</param>
/// <param name="Reasons">The reasons, each once, in the order they were found.</param>
public sealed record FraudAssessment(int Score, IReadOnlyList<FraudReasonCode> Reasons)
{
    /// <summary>The highest score; a sum of weights is held to it.</summary>
    public const int MaxScore = 100;

    /// <summary>An assessment that saw no risk.</summary>
    public static readonly FraudAssessment None = new(0, []);

    /// <summary>Adds up the weights of the signals that fired.</summary>
    /// <param name="hits">The signals that fired.</param>
    /// <returns>The sum clamped to 0 to <see cref="MaxScore"/>, with each hit's reason once.</returns>
    public static FraudAssessment FromHits(IEnumerable<FraudSignalHit> hits)
    {
        ArgumentNullException.ThrowIfNull(hits);

        var list = hits.ToList();
        var sum = list.Sum(h => h.Weight);
        return new FraudAssessment(Math.Clamp(sum, 0, MaxScore), list.Select(h => h.Reason).Distinct().ToList().AsReadOnly());
    }

    /// <summary>Joins two assessments of the same request: the higher score counts, and the reasons of both are kept.</summary>
    /// <param name="other">The other assessment.</param>
    /// <returns>The combined assessment.</returns>
    public FraudAssessment CombineWith(FraudAssessment other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return new FraudAssessment(Math.Max(Score, other.Score), Reasons.Concat(other.Reasons).Distinct().ToList().AsReadOnly());
    }
}
