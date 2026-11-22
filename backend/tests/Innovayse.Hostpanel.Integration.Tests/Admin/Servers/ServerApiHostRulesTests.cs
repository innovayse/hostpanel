using Innovayse.Hostpanel.Application.Admin.Servers.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary>
/// <see cref="ServerApiHostRules"/>: the one definition of an API address. A blank address is no address, a host name or an IP
/// address is well formed, anything else (a scheme, a path, a port, credentials, a query, brackets, spaces) is not, and an IP
/// address the host never dials is refused. No container or database.
/// </summary>
public sealed class ServerApiHostRulesTests
{
    /// <summary>Blank, a host name and an IP address of either family are well formed.</summary>
    /// <param name="value">The address as typed.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("panel.example.com")]
    [InlineData("panel-1")]
    [InlineData("10.77.0.1")]
    [InlineData("2001:db8::1")]
    public void IsWellFormed_AcceptsBlankAHostNameAndAnIpAddress(string? value)
    {
        ServerApiHostRules.IsWellFormed(value).Should().BeTrue();
    }

    /// <summary>A scheme, path, port, credentials, query, fragment, bracket, zone id, space or bad label is refused.</summary>
    /// <param name="value">The address as typed.</param>
    [Theory]
    [InlineData("https://panel.example.com")]
    [InlineData("panel.example.com/path")]
    [InlineData("panel.example.com:2087")]
    [InlineData("user@panel.example.com")]
    [InlineData("panel.example.com?x=1")]
    [InlineData("panel.example.com#top")]
    [InlineData("[2001:db8::1]")]
    [InlineData("fe80::1%eth0")]
    [InlineData("panel example.com")]
    [InlineData("-panel.example.com")]
    [InlineData("panel..example.com")]
    [InlineData("256.1.1.1")]
    public void IsWellFormed_RefusesAnythingThatIsNotAHostNameOrAnIpAddress(string value)
    {
        ServerApiHostRules.IsWellFormed(value).Should().BeFalse();
    }

    /// <summary>A host name longer than the limit is refused, and one at the limit is not.</summary>
    [Fact]
    public void IsWellFormed_RefusesAHostNameOverTheLimit()
    {
        var atLimit = string.Join('.', Enumerable.Repeat(new string('a', 61), 5))[..ServerApiHostRules.MaxLength];

        ServerApiHostRules.IsWellFormed(atLimit).Should().BeTrue();
        ServerApiHostRules.IsWellFormed(atLimit + "a").Should().BeFalse();
    }

    /// <summary>Link-local, cloud metadata, loopback and unspecified IP addresses are never dialled; private and public ones are.</summary>
    /// <param name="value">The address as typed.</param>
    /// <param name="dialable">Whether the host may dial it.</param>
    [Theory]
    [InlineData("169.254.169.254", false)]
    [InlineData("169.254.1.1", false)]
    [InlineData("100.100.100.200", false)]
    [InlineData("fd00:ec2::254", false)]
    [InlineData("fe80::1", false)]
    [InlineData("127.0.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("10.77.0.1", true)]
    [InlineData("192.168.1.10", true)]
    [InlineData("203.0.113.9", true)]
    [InlineData("panel.example.com", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    public void IsDialable_RefusesOnlyAnIpAddressThatIsNeverDialled(string? value, bool dialable)
    {
        ServerApiHostRules.IsDialable(value).Should().Be(dialable);
    }
}
