using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Domain.Fraud;

/// <summary>The one rule that turns a score into what happens to the request.</summary>
public static class FraudDecisionPolicy
{
    /// <summary>Decides from a score and the operator's two thresholds.</summary>
    /// <param name="score">The risk score, 0 to 100.</param>
    /// <param name="manualReviewAt">The score from which staff review the order.</param>
    /// <param name="autoRejectAt">The score from which the request is refused.</param>
    /// <returns><see cref="FraudDecision.Rejected"/> at or above the reject threshold, <see cref="FraudDecision.Review"/> at or above the review threshold, <see cref="FraudDecision.Approved"/> below it.</returns>
    public static FraudDecision Decide(int score, int manualReviewAt, int autoRejectAt)
    {
        if (score >= autoRejectAt)
        {
            return FraudDecision.Rejected;
        }

        return score >= manualReviewAt ? FraudDecision.Review : FraudDecision.Approved;
    }
}
