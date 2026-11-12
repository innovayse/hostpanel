using Innovayse.Hostpanel.Domain.Fraud;
using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Domain.Fraud.Signals;

namespace Innovayse.Hostpanel.Domain.Tests.Fraud;

/// <summary>Unit tests for <see cref="BlockedEmailDomainSignal"/>.</summary>
public sealed class BlockedEmailDomainSignalTests
{
    /// <summary>The listed domain and its subdomains fire; a domain that merely ends with the same letters does not.</summary>
    /// <param name="email">The e-mail address.</param>
    /// <param name="fires">Whether the signal fires.</param>
    [Theory]
    [InlineData("a@spam.example", false)]
    [InlineData("a@bad.com", true)]
    [InlineData("a@BAD.com", true)]
    [InlineData("a@mail.bad.com", true)]
    [InlineData("a@notbad.com", false)]
    [InlineData("a@bad.com.au", false)]
    public void Evaluate_MatchesTheDomainAndItsSubdomains(string email, bool fires)
    {
        var context = FraudContexts.Request(email: email, blockList: FraudContexts.Listing((BlockListType.EmailDomain, "bad.com")));

        var hit = new BlockedEmailDomainSignal().Evaluate(context);

        (hit is not null).Should().Be(fires);
        hit?.Reason.Should().Be(FraudReasonCode.BlockedEmailDomain);
        hit?.Weight.Should().Be(FraudAssessment.MaxScore);
    }

    /// <summary>A missing or malformed address has no domain, so nothing fires.</summary>
    /// <param name="email">The e-mail address.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("no-at-sign")]
    [InlineData("trailing@")]
    public void Evaluate_NoDomain_DoesNotFire(string? email)
    {
        var context = FraudContexts.Request(email: email, blockList: FraudContexts.Listing((BlockListType.EmailDomain, "bad.com")));

        new BlockedEmailDomainSignal().Evaluate(context).Should().BeNull();
    }
}
