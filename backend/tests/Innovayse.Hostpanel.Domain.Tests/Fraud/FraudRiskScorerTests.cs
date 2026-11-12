using Innovayse.Hostpanel.Domain.Fraud;
using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Domain.Fraud.Signals;

namespace Innovayse.Hostpanel.Domain.Tests.Fraud;

/// <summary>Unit tests for <see cref="FraudRiskScorer"/> over the product's own signal list.</summary>
public sealed class FraudRiskScorerTests
{
    /// <summary>A harmless request scores nothing.</summary>
    [Fact]
    public void Assess_HarmlessRequest_ScoresZero()
    {
        FraudRiskScorer.Assess(FraudContexts.Request()).Should().BeEquivalentTo(FraudAssessment.None);
    }

    /// <summary>A block list hit alone reaches any reject threshold.</summary>
    [Fact]
    public void Assess_BlockListHit_ScoresTheMaximum()
    {
        var context = FraudContexts.Request(blockList: FraudContexts.Listing((BlockListType.Ip, "203.0.113.9")));

        var assessment = FraudRiskScorer.Assess(context);

        assessment.Score.Should().Be(FraudAssessment.MaxScore);
        assessment.Reasons.Should().Equal(FraudReasonCode.BlockedIp);
    }

    /// <summary>Several weak signals add up, with every reason named once.</summary>
    [Fact]
    public void Assess_SeveralSignals_AddUp()
    {
        var context = FraudContexts.Request(
            email: "x@mailinator.com", billingCountry: "AM", ipCountry: "US", otherAccounts: 3, orders: 3);

        var assessment = FraudRiskScorer.Assess(context);

        assessment.Score.Should().Be(100);
        assessment.Reasons.Should().BeEquivalentTo(
        [
            FraudReasonCode.DisposableEmail,
            FraudReasonCode.SharedIpAccounts,
            FraudReasonCode.OrderVelocity,
            FraudReasonCode.CountryMismatch,
        ]);
    }

    /// <summary>The scorer runs exactly the signals it is given, so a signal added to the list is picked up and one left out is not.</summary>
    [Fact]
    public void Assess_RunsTheGivenSignals()
    {
        var context = FraudContexts.Request(email: "x@mailinator.com");

        FraudRiskScorer.Assess(context, [new VelocitySignal()]).Score.Should().Be(0);
        FraudRiskScorer.Assess(context, [new DisposableEmailSignal()]).Score.Should().Be(DisposableEmailSignal.Weight);
    }

    /// <summary>Every signal class in the Domain assembly is in the list the product runs.</summary>
    [Fact]
    public void FraudSignals_ListEverySignal()
    {
        var implemented = typeof(IFraudSignal).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IFraudSignal).IsAssignableFrom(t))
            .ToList();

        FraudSignals.All.Select(s => s.GetType()).Should().BeEquivalentTo(implemented);
    }
}
