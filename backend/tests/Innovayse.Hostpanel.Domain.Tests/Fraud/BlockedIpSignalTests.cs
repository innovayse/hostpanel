using Innovayse.Hostpanel.Domain.Fraud;
using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Domain.Fraud.Signals;

namespace Innovayse.Hostpanel.Domain.Tests.Fraud;

/// <summary>Unit tests for <see cref="BlockedIpSignal"/>.</summary>
public sealed class BlockedIpSignalTests
{
    /// <summary>A listed single address fires with the strongest weight.</summary>
    [Fact]
    public void Evaluate_ListedAddress_Fires()
    {
        var context = FraudContexts.Request(blockList: FraudContexts.Listing((BlockListType.Ip, "203.0.113.9")));

        var hit = new BlockedIpSignal().Evaluate(context);

        hit.Should().Be(new FraudSignalHit(FraudReasonCode.BlockedIp, FraudAssessment.MaxScore));
    }

    /// <summary>An address inside a listed range fires; one outside does not.</summary>
    /// <param name="ip">The caller's address.</param>
    /// <param name="fires">Whether the signal fires.</param>
    [Theory]
    [InlineData("203.0.113.200", true)]
    [InlineData("203.0.114.1", false)]
    public void Evaluate_ListedRange_FiresInsideOnly(string ip, bool fires)
    {
        var context = FraudContexts.Request(ip: ip, blockList: FraudContexts.Listing((BlockListType.Cidr, "203.0.113.0/24")));

        (new BlockedIpSignal().Evaluate(context) is not null).Should().Be(fires);
    }

    /// <summary>An IPv4 address mapped into IPv6 matches the listed IPv4 address.</summary>
    [Fact]
    public void Evaluate_MappedIpv4_MatchesTheListedAddress()
    {
        var context = FraudContexts.Request(ip: "::ffff:203.0.113.9", blockList: FraudContexts.Listing((BlockListType.Ip, "203.0.113.9")));

        new BlockedIpSignal().Evaluate(context).Should().NotBeNull();
    }

    /// <summary>An unknown or unreadable address and entries of other types never fire it.</summary>
    [Fact]
    public void Evaluate_NoAddressOrOtherTypes_DoesNotFire()
    {
        var other = FraudContexts.Listing((BlockListType.EmailDomain, "203.example.com"), (BlockListType.Country, "AM"));

        new BlockedIpSignal().Evaluate(FraudContexts.Request(ip: null, blockList: FraudContexts.Listing((BlockListType.Ip, "203.0.113.9")))).Should().BeNull();
        new BlockedIpSignal().Evaluate(FraudContexts.Request(ip: "garbage", blockList: FraudContexts.Listing((BlockListType.Ip, "203.0.113.9")))).Should().BeNull();
        new BlockedIpSignal().Evaluate(FraudContexts.Request(blockList: other)).Should().BeNull();
    }
}
