using Innovayse.Hostpanel.Domain.Ssh;

namespace Innovayse.Hostpanel.Domain.Tests.Ssh;

/// <summary>
/// <see cref="SshAllowedFromRules"/>: the sources an SSH key may be limited to, the same cases the agent's own tests
/// (<c>backend/agent/ssh-key-agent/tests</c>) accept and refuse.
/// </summary>
public sealed class SshAllowedFromRulesTests
{
    /// <summary>Plain addresses and networks with zero host bits are valid sources.</summary>
    /// <param name="source">The source.</param>
    [Theory]
    [InlineData("203.0.113.7")]
    [InlineData("0.0.0.0/0")]
    [InlineData("198.51.100.0/24")]
    [InlineData("203.0.113.7/32")]
    [InlineData("255.255.255.255")]
    [InlineData("2001:db8::1")]
    [InlineData("2001:DB8:1::/48")]
    [InlineData("::/0")]
    [InlineData("::1")]
    [InlineData("2001:db8:0:0:0:0:0:1")]
    public void IsValidSource_AddressOrNetwork_IsAccepted(string source)
    {
        SshAllowedFromRules.IsValidSource(source).Should().BeTrue();
    }

    /// <summary>Host names, wildcards, negations, bad shapes and out-of-range numbers are refused.</summary>
    /// <param name="source">The source.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("example.com")]
    [InlineData("*.example.com")]
    [InlineData("!203.0.113.7")]
    [InlineData("203.0.113.256")]
    [InlineData("203.0.113")]
    [InlineData("010.0.0.1")]
    [InlineData("203.0.113.7 ")]
    [InlineData("203.0.113.7\n")]
    [InlineData("203.0.113.7/")]
    [InlineData("203.0.113.0/033")]
    [InlineData("203.0.113.0/-1")]
    [InlineData("1.2.3.4/33")]
    [InlineData("2001:db8::/129")]
    [InlineData("198.51.100.5/24")]
    [InlineData("2001:db8::1/64")]
    [InlineData("::1.2.3.4")]
    [InlineData("fe80::1%eth0")]
    [InlineData("1.2.3.4\"")]
    [InlineData("1:2")]
    [InlineData("12345::1")]
    public void IsValidSource_AnythingElse_IsRefused(string? source)
    {
        SshAllowedFromRules.IsValidSource(source).Should().BeFalse();
    }

    /// <summary>A list of one to sixteen distinct valid sources is valid.</summary>
    [Fact]
    public void IsValidList_OneToSixteenDistinctSources_IsAccepted()
    {
        SshAllowedFromRules.IsValidList(["203.0.113.7"]).Should().BeTrue();
        SshAllowedFromRules.IsValidList(["203.0.113.7", "2001:db8::/32"]).Should().BeTrue();
        SshAllowedFromRules.IsValidList(Enumerable.Range(1, SshAllowedFromRules.MaxEntries).Select(n => $"10.0.0.{n}").ToArray())
            .Should().BeTrue();
    }

    /// <summary>No list, an empty list, too many entries, a repeat (without case) and a bad entry are refused.</summary>
    [Fact]
    public void IsValidList_NoneTooManyRepeatedOrBad_IsRefused()
    {
        SshAllowedFromRules.IsValidList(null).Should().BeFalse();
        SshAllowedFromRules.IsValidList([]).Should().BeFalse();
        SshAllowedFromRules.IsValidList(Enumerable.Range(1, SshAllowedFromRules.MaxEntries + 1).Select(n => $"10.0.0.{n}").ToArray())
            .Should().BeFalse();
        SshAllowedFromRules.IsValidList(["203.0.113.7", "203.0.113.7"]).Should().BeFalse();
        SshAllowedFromRules.IsValidList(["2001:db8::/32", "2001:DB8::/32"]).Should().BeFalse();
        SshAllowedFromRules.IsValidList(["203.0.113.7", "example.com"]).Should().BeFalse();
    }
}
