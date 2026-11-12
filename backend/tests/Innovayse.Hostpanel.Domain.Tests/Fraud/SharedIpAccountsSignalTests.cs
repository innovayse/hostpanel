using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Domain.Fraud.Signals;

namespace Innovayse.Hostpanel.Domain.Tests.Fraud;

/// <summary>Unit tests for <see cref="SharedIpAccountsSignal"/>.</summary>
public sealed class SharedIpAccountsSignalTests
{
    /// <summary>The signal fires once the other accounts seen reach the limit, so this one would be past it.</summary>
    /// <param name="others">Other e-mail addresses seen from the address.</param>
    /// <param name="fires">Whether the signal fires with a limit of three.</param>
    [Theory]
    [InlineData(0, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(10, true)]
    public void Evaluate_FiresAtTheLimit(int others, bool fires)
    {
        var hit = new SharedIpAccountsSignal().Evaluate(FraudContexts.Request(otherAccounts: others, maxAccounts: 3));

        (hit is not null).Should().Be(fires);
        hit?.Reason.Should().Be(FraudReasonCode.SharedIpAccounts);
    }

    /// <summary>Without an address there is nothing to count against.</summary>
    [Fact]
    public void Evaluate_NoAddress_DoesNotFire()
    {
        new SharedIpAccountsSignal().Evaluate(FraudContexts.Request(ip: null, otherAccounts: 9)).Should().BeNull();
    }
}
