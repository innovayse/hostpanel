using System.Net;
using Innovayse.Hostpanel.Domain.Fraud;

namespace Innovayse.Hostpanel.Domain.Tests.Fraud;

/// <summary>Unit tests for CIDR parsing and matching in <see cref="IpNetwork"/>.</summary>
public sealed class IpNetworkTests
{
    /// <summary>An address inside an IPv4 range is contained; one outside is not.</summary>
    /// <param name="address">The address to test.</param>
    /// <param name="expected">Whether it lies in <c>203.0.113.0/24</c>.</param>
    [Theory]
    [InlineData("203.0.113.0", true)]
    [InlineData("203.0.113.255", true)]
    [InlineData("203.0.114.0", false)]
    [InlineData("10.0.0.1", false)]
    public void Contains_Ipv4Range_MatchesByPrefix(string address, bool expected)
    {
        IpNetwork.TryParse("203.0.113.0/24", out var network).Should().BeTrue();

        network.Contains(IPAddress.Parse(address)).Should().Be(expected);
    }

    /// <summary>A prefix that is not a multiple of eight bits still masks the right bits.</summary>
    [Fact]
    public void Contains_UnalignedPrefix_MasksBits()
    {
        IpNetwork.TryParse("10.0.0.0/20", out var network).Should().BeTrue();

        network.Contains(IPAddress.Parse("10.0.15.255")).Should().BeTrue();
        network.Contains(IPAddress.Parse("10.0.16.0")).Should().BeFalse();
    }

    /// <summary>An IPv6 range matches IPv6 addresses by prefix.</summary>
    [Fact]
    public void Contains_Ipv6Range_MatchesByPrefix()
    {
        IpNetwork.TryParse("2001:db8::/32", out var network).Should().BeTrue();

        network.Contains(IPAddress.Parse("2001:db8:ffff::1")).Should().BeTrue();
        network.Contains(IPAddress.Parse("2001:db9::1")).Should().BeFalse();
    }

    /// <summary>An address of the other family is never inside a range.</summary>
    [Fact]
    public void Contains_OtherFamily_IsFalse()
    {
        IpNetwork.TryParse("0.0.0.0/0", out var everyIpv4).Should().BeTrue();

        everyIpv4.Contains(IPAddress.Parse("2001:db8::1")).Should().BeFalse();
    }

    /// <summary>An IPv4 address mapped into IPv6 is tested as the IPv4 address.</summary>
    [Fact]
    public void Contains_MappedIpv4_IsTestedAsIpv4()
    {
        IpNetwork.TryParse("203.0.113.0/24", out var network).Should().BeTrue();

        network.Contains(IPAddress.Parse("::ffff:203.0.113.9")).Should().BeTrue();
    }

    /// <summary>Host bits typed in the address are cleared, so the stored spelling is canonical.</summary>
    [Fact]
    public void TryParse_HostBitsSet_AreCleared()
    {
        IpNetwork.TryParse("203.0.113.77/24", out var network).Should().BeTrue();

        network.ToString().Should().Be("203.0.113.0/24");
    }

    /// <summary>Text that is not a range is refused.</summary>
    /// <param name="text">The text to parse.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("203.0.113.0")]
    [InlineData("203.0.113.0/33")]
    [InlineData("203.0.113.0/-1")]
    [InlineData("203.0.113.0/x")]
    [InlineData("not-an-ip/8")]
    [InlineData("2001:db8::/129")]
    public void TryParse_Malformed_IsRefused(string? text)
    {
        IpNetwork.TryParse(text, out _).Should().BeFalse();
    }
}
