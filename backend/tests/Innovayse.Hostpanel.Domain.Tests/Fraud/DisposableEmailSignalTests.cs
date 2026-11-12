using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Domain.Fraud.Signals;

namespace Innovayse.Hostpanel.Domain.Tests.Fraud;

/// <summary>Unit tests for <see cref="DisposableEmailSignal"/> and the list it reads.</summary>
public sealed class DisposableEmailSignalTests
{
    /// <summary>A disposable mailbox domain or one of its subdomains fires; an ordinary domain does not.</summary>
    /// <param name="email">The e-mail address.</param>
    /// <param name="fires">Whether the signal fires.</param>
    [Theory]
    [InlineData("x@mailinator.com", true)]
    [InlineData("x@MAILINATOR.COM", true)]
    [InlineData("x@sub.mailinator.com", true)]
    [InlineData("x@gmail.com", false)]
    [InlineData("x@notmailinator.com", false)]
    [InlineData(null, false)]
    public void Evaluate_FiresForDisposableDomains(string? email, bool fires)
    {
        var hit = new DisposableEmailSignal().Evaluate(FraudContexts.Request(email: email));

        (hit is not null).Should().Be(fires);
        hit?.Reason.Should().Be(FraudReasonCode.DisposableEmail);
    }

    /// <summary>With the default review threshold a disposable address alone holds an order for review, and does not refuse it.</summary>
    [Fact]
    public void Weight_ReachesTheDefaultReviewThresholdOnly()
    {
        DisposableEmailSignal.Weight.Should().BeGreaterThanOrEqualTo(50).And.BeLessThan(90);
    }

    /// <summary>The built-in list is written in lower case, so the comparison against a lower-cased domain holds.</summary>
    [Fact]
    public void List_IsLowerCase()
    {
        DisposableEmailDomains.All.Should().OnlyContain(domain => domain == domain.ToLowerInvariant());
    }
}
