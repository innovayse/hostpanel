using System.Net;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Infrastructure.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Security;

/// <summary>
/// <see cref="OutboundAddressPolicy"/>: loopback, link-local and metadata addresses are never
/// dialled, private ranges are told apart from public ones.
/// </summary>
public sealed class OutboundAddressPolicyTests
{
    /// <summary>Addresses no outbound client may ever dial.</summary>
    /// <param name="address">The address under test.</param>
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("169.254.169.254")]
    [InlineData("fd00:ec2::254")]
    [InlineData("100.100.100.200")]
    [InlineData("0.0.0.0")]
    [InlineData("fe80::1")]
    [InlineData("224.0.0.1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::")]
    [InlineData("255.255.255.255")]
    public void Classify_LoopbackLinkLocalAndMetadata_AreRefused(string address)
    {
        OutboundAddressPolicy.Classify(IPAddress.Parse(address)).Should().Be(OutboundAddressClass.Refused);
    }

    /// <summary>Private network addresses are private, not refused.</summary>
    /// <param name="address">The address under test.</param>
    [Theory]
    [InlineData("10.0.0.5")]
    [InlineData("172.16.4.2")]
    [InlineData("192.168.1.10")]
    [InlineData("100.64.0.1")]
    [InlineData("fd12:3456::1")]
    [InlineData("172.31.255.255")]
    [InlineData("fd00::1")]
    [InlineData("::ffff:10.0.0.1")]
    public void Classify_PrivateRanges_ArePrivate(string address)
    {
        OutboundAddressPolicy.Classify(IPAddress.Parse(address)).Should().Be(OutboundAddressClass.Private);
    }

    /// <summary>Ordinary internet addresses are public.</summary>
    /// <param name="address">The address under test.</param>
    [Theory]
    [InlineData("93.184.216.34")]
    [InlineData("8.8.8.8")]
    [InlineData("2606:4700::1111")]
    [InlineData("172.32.0.1")]
    [InlineData("2606:4700:4700::1111")]
    public void Classify_InternetAddresses_ArePublic(string address)
    {
        OutboundAddressPolicy.Classify(IPAddress.Parse(address)).Should().Be(OutboundAddressClass.Public);
    }
}
