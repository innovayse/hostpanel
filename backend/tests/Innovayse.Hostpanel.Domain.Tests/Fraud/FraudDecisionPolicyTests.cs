using Innovayse.Hostpanel.Domain.Fraud;
using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Fraud;

/// <summary>Unit tests for the thresholds in <see cref="FraudDecisionPolicy"/>.</summary>
public sealed class FraudDecisionPolicyTests
{
    /// <summary>A score below the review threshold is approved, from it up to the reject threshold it is reviewed, from there it is rejected.</summary>
    /// <param name="score">The score.</param>
    /// <param name="expected">The decision with the default thresholds 50 and 90.</param>
    [Theory]
    [InlineData(0, FraudDecision.Approved)]
    [InlineData(49, FraudDecision.Approved)]
    [InlineData(50, FraudDecision.Review)]
    [InlineData(89, FraudDecision.Review)]
    [InlineData(90, FraudDecision.Rejected)]
    [InlineData(100, FraudDecision.Rejected)]
    public void Decide_FollowsTheThresholds(int score, FraudDecision expected)
    {
        FraudDecisionPolicy.Decide(score, manualReviewAt: 50, autoRejectAt: 90).Should().Be(expected);
    }
}
