using Innovayse.Hostpanel.Domain.Fraud;
using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Fraud;

/// <summary>Unit tests for the <see cref="FraudLogEntry"/> aggregate.</summary>
public sealed class FraudLogEntryTests
{
    /// <summary>Create keeps the decision and its facts, lower-cases the e-mail address and exposes the reasons.</summary>
    [Fact]
    public void Create_KeepsTheDecisionAndItsFacts()
    {
        var entry = FraudLogEntry.Create(
            FraudCheckKind.Order, FraudDecision.Review, 7, 11, " 203.0.113.9 ", " Buyer@Example.COM ", 60, [FraudReasonCode.DisposableEmail]);

        entry.Kind.Should().Be(FraudCheckKind.Order);
        entry.Decision.Should().Be(FraudDecision.Review);
        entry.ClientId.Should().Be(7);
        entry.OrderId.Should().Be(11);
        entry.IpAddress.Should().Be("203.0.113.9");
        entry.Email.Should().Be("buyer@example.com");
        entry.Score.Should().Be(60);
        entry.Reasons.Should().Equal(FraudReasonCode.DisposableEmail);
    }

    /// <summary>A blank address or e-mail is stored as unknown.</summary>
    [Fact]
    public void Create_BlankIpAndEmail_AreStoredAsNull()
    {
        var entry = FraudLogEntry.Create(FraudCheckKind.Registration, FraudDecision.Approved, null, null, " ", "", 0, []);

        entry.IpAddress.Should().BeNull();
        entry.Email.Should().BeNull();
    }

    /// <summary>A score outside 0 to 100 is refused.</summary>
    /// <param name="score">The score.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_ScoreOutsideTheScale_Throws(int score)
    {
        var act = () => FraudLogEntry.Create(FraudCheckKind.Order, FraudDecision.Approved, null, null, null, null, score, []);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
