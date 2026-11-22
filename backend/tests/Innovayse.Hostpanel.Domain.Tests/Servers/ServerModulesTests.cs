using Innovayse.Hostpanel.Domain.Servers;

namespace Innovayse.Hostpanel.Domain.Tests.Servers;

/// <summary>
/// <see cref="ServerModules"/>: the host offers cPanel only, the ids it still names are spelled exactly as stored rows hold them,
/// and two ids are the same module when they match ignoring case, the one comparison every caller uses.
/// </summary>
public sealed class ServerModulesTests
{
    /// <summary>The only module offered without a panel plugin is cPanel, in its stored spelling.</summary>
    [Fact]
    public void Known_IsCPanelOnly()
    {
        ServerModules.Known.Should().Equal("CPanel");
    }

    /// <summary>The legacy ids are spelled as stored rows hold them and are not offered.</summary>
    [Fact]
    public void LegacyConstants_AreTheStoredSpellings_AndAreNotKnown()
    {
        ServerModules.Centos.Should().Be("Centos");
        ServerModules.Cwp7.Should().Be("Cwp7");
        ServerModules.Known.Should().NotContain([ServerModules.Centos, ServerModules.Cwp7]);
    }

    /// <summary>Ids match ignoring case, so a row stored as <c>HestiaCP</c> and a plugin that declares <c>hestiacp</c> are one module.</summary>
    /// <param name="a">The first id.</param>
    /// <param name="b">The second id.</param>
    [Theory]
    [InlineData("HestiaCP", "hestiacp")]
    [InlineData("CPANEL", "cpanel")]
    [InlineData("hestiacp", "hestiacp")]
    public void Same_IgnoresCase(string a, string b)
    {
        ServerModules.Same(a, b).Should().BeTrue();
    }

    /// <summary>Different ids, and a missing id on either side, are never the same module.</summary>
    /// <param name="a">The first id, or null.</param>
    /// <param name="b">The second id, or null.</param>
    [Theory]
    [InlineData("HestiaCP", "cpanel")]
    [InlineData("HestiaCP", "HestiaCP ")]
    [InlineData(null, "hestiacp")]
    [InlineData("hestiacp", null)]
    [InlineData(null, null)]
    public void Same_DifferentOrMissingIds_AreNotTheSame(string? a, string? b)
    {
        ServerModules.Same(a, b).Should().BeFalse();
    }
}
