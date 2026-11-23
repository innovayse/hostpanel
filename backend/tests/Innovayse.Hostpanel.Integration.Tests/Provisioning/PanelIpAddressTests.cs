using Innovayse.Hostpanel.Application.Provisioning.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// <see cref="PanelIpAddress"/>: the one check an address a panel reports must pass before the platform keeps it.
/// </summary>
public sealed class PanelIpAddressTests
{
    /// <summary>An IPv4 or IPv6 address is read in its canonical text form, white space trimmed.</summary>
    /// <param name="value">What the panel reported.</param>
    /// <param name="expected">The canonical form.</param>
    [Theory]
    [InlineData("203.0.113.9", "203.0.113.9")]
    [InlineData("  203.0.113.9 ", "203.0.113.9")]
    [InlineData("2001:0DB8:0:0:0:0:0:1", "2001:db8::1")]
    [InlineData("::1", "::1")]
    public void Normalize_AnAddress_IsReadCanonically(string value, string expected)
    {
        PanelIpAddress.Normalize(value).Should().Be(expected);
    }

    /// <summary>Anything that is no address, or does not fit the stored column, is left out.</summary>
    /// <param name="value">What the panel reported.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1")]
    [InlineData("12345")]
    [InlineData("example.com")]
    [InlineData("203.0.113.999")]
    [InlineData("203.0.113.9; drop table")]
    public void Normalize_NotAnAddress_IsNull(string? value)
    {
        PanelIpAddress.Normalize(value).Should().BeNull();
    }

    /// <summary>A text longer than the column is left out even when it begins like an address.</summary>
    [Fact]
    public void Normalize_TooLong_IsNull()
    {
        PanelIpAddress.Normalize("203.0.113.9" + new string(' ', 60) + "x").Should().BeNull();
    }
}
