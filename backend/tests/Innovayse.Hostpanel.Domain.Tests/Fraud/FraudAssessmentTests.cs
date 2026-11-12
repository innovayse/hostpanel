using Innovayse.Hostpanel.Domain.Fraud;
using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Domain.Fraud.Signals;

namespace Innovayse.Hostpanel.Domain.Tests.Fraud;

/// <summary>Unit tests for adding up and combining scores in <see cref="FraudAssessment"/>.</summary>
public sealed class FraudAssessmentTests
{
    /// <summary>The weights add up, and each reason is reported once.</summary>
    [Fact]
    public void FromHits_SumsTheWeights()
    {
        var assessment = FraudAssessment.FromHits(
        [
            new FraudSignalHit(FraudReasonCode.DisposableEmail, 20),
            new FraudSignalHit(FraudReasonCode.OrderVelocity, 15),
            new FraudSignalHit(FraudReasonCode.DisposableEmail, 5),
        ]);

        assessment.Score.Should().Be(40);
        assessment.Reasons.Should().Equal(FraudReasonCode.DisposableEmail, FraudReasonCode.OrderVelocity);
    }

    /// <summary>A sum past 100 is held to 100.</summary>
    [Fact]
    public void FromHits_ClampsToTheScale()
    {
        var assessment = FraudAssessment.FromHits(
        [
            new FraudSignalHit(FraudReasonCode.BlockedIp, 100),
            new FraudSignalHit(FraudReasonCode.DisposableEmail, 60),
        ]);

        assessment.Score.Should().Be(FraudAssessment.MaxScore);
    }

    /// <summary>No hit is no risk.</summary>
    [Fact]
    public void FromHits_NoHits_IsNone()
    {
        FraudAssessment.FromHits([]).Should().BeEquivalentTo(FraudAssessment.None);
    }

    /// <summary>Combining takes the higher score and keeps the reasons of both once.</summary>
    [Fact]
    public void CombineWith_TakesTheMaxScoreAndMergesTheReasons()
    {
        var local = new FraudAssessment(40, [FraudReasonCode.DisposableEmail, FraudReasonCode.CountryMismatch]);
        var external = new FraudAssessment(75, [FraudReasonCode.ProviderHighRisk, FraudReasonCode.CountryMismatch]);

        var combined = local.CombineWith(external);

        combined.Score.Should().Be(75);
        combined.Reasons.Should().Equal(
            FraudReasonCode.DisposableEmail, FraudReasonCode.CountryMismatch, FraudReasonCode.ProviderHighRisk);
    }

    /// <summary>The lower score does not pull the combination down.</summary>
    [Fact]
    public void CombineWith_ALowerExternalScore_KeepsTheLocalScore()
    {
        var combined = new FraudAssessment(70, [FraudReasonCode.OrderVelocity]).CombineWith(new FraudAssessment(10, []));

        combined.Score.Should().Be(70);
    }
}
