using Innovayse.Hostpanel.Domain.Servers;

namespace Innovayse.Hostpanel.Domain.Tests.Servers;

/// <summary>
/// <see cref="ServerModules"/>: the ids the host itself knows are spelled exactly as rows saved before modules became text hold them,
/// and two ids are the same module when they match ignoring case, the one comparison every caller uses.
/// </summary>
public sealed class ServerModulesTests
{
    /// <summary>The known ids are the names the closed list used to store, in the order the server form offered them.</summary>
    [Fact]
    public void Known_IsTheStoredSpellingsInTheFormsOrder()
    {
        ServerModules.Known.Should().Equal("CPanel", "DirectAdmin", "Centos", "Cwp7", "Lukittu", "Virtualmin", "HyperVm");
    }

    /// <summary>Each constant is the stored spelling of its module and is among the known ids.</summary>
    [Fact]
    public void Constants_AreTheStoredSpellings()
    {
        new[]
            {
                ServerModules.CPanel, ServerModules.DirectAdmin, ServerModules.Centos,
                ServerModules.Cwp7, ServerModules.Lukittu, ServerModules.Virtualmin, ServerModules.HyperVm,
            }
            .Should().BeEquivalentTo(ServerModules.Known);
    }

    /// <summary>Ids match ignoring case, so a row stored as <c>DirectAdmin</c> and a plugin that declares <c>directadmin</c> are one module.</summary>
    /// <param name="a">The first id.</param>
    /// <param name="b">The second id.</param>
    [Theory]
    [InlineData("DirectAdmin", "directadmin")]
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
    [InlineData("DirectAdmin", "cpanel")]
    [InlineData("DirectAdmin", "DirectAdmin ")]
    [InlineData(null, "directadmin")]
    [InlineData("directadmin", null)]
    [InlineData(null, null)]
    public void Same_DifferentOrMissingIds_AreNotTheSame(string? a, string? b)
    {
        ServerModules.Same(a, b).Should().BeFalse();
    }
}
