using Innovayse.Hostpanel.Domain.Plugins;

namespace Innovayse.Hostpanel.Domain.Tests.Plugins;

/// <summary>The plugin id rule: plain folder-safe names pass, anything a path could read as structure fails.</summary>
public sealed class PluginIdentifierTests
{
    /// <summary>Lower-case words joined by single hyphens are accepted.</summary>
    /// <param name="id">The candidate id.</param>
    [Theory]
    [InlineData("inecobank")]
    [InlineData("test-plugin-0123abcd")]
    [InlineData("a")]
    [InlineData("pay2-go")]
    public void AcceptsPlainIds(string id)
    {
        PluginIdentifier.IsValid(id).Should().BeTrue();
    }

    /// <summary>Empty values, separators, parent references, absolute paths and odd casing are refused.</summary>
    /// <param name="id">The candidate id.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../outside")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("/etc")]
    [InlineData("C:")]
    [InlineData("Upper")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("double--hyphen")]
    [InlineData("with space")]
    [InlineData("dot.name")]
    public void RefusesUnsafeIds(string? id)
    {
        PluginIdentifier.IsValid(id).Should().BeFalse();
    }

    /// <summary>An id longer than <see cref="PluginIdentifier.MaxLength"/> is refused.</summary>
    [Fact]
    public void RefusesOverlongIds()
    {
        PluginIdentifier.IsValid(new string('a', PluginIdentifier.MaxLength)).Should().BeTrue();
        PluginIdentifier.IsValid(new string('a', PluginIdentifier.MaxLength + 1)).Should().BeFalse();
    }

    /// <summary>
    /// A reserved id is refused although it has the accepted shape: <c>ui</c> is the panels' plugin index segment and
    /// <c>slots</c> the slot index segment, and each would shadow the routes of a plugin with that id. An id that only
    /// starts with one of them is a normal id.
    /// </summary>
    /// <param name="id">A reserved id.</param>
    [Theory]
    [InlineData("ui")]
    [InlineData("slots")]
    public void RefusesReservedIds(string id)
    {
        PluginIdentifier.Reserved.Should().Contain(id);
        PluginIdentifier.IsValid(id).Should().BeFalse();
        PluginIdentifier.IsValid($"{id}-kit").Should().BeTrue();
    }

    /// <summary>
    /// A reserved id still has the plugin id shape and is reported as reserved, so listing and removal (which ask
    /// <see cref="PluginIdentifier.IsWellFormed"/>) still reach a plugin installed before the id was reserved.
    /// </summary>
    [Fact]
    public void ReservedIds_AreWellFormedAndReserved()
    {
        PluginIdentifier.IsWellFormed("ui").Should().BeTrue();
        PluginIdentifier.IsReserved("ui").Should().BeTrue();
        PluginIdentifier.IsWellFormed("slots").Should().BeTrue();
        PluginIdentifier.IsReserved("slots").Should().BeTrue();
        PluginIdentifier.IsReserved("ui-kit").Should().BeFalse();
        PluginIdentifier.IsReserved("slot").Should().BeFalse();
        PluginIdentifier.IsReserved(null).Should().BeFalse();
    }

    /// <summary>The shape check refuses everything the full rule refuses for its shape.</summary>
    /// <param name="id">The candidate id.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("../outside")]
    [InlineData("a/b")]
    [InlineData("Upper")]
    [InlineData("double--hyphen")]
    public void IsWellFormed_RefusesUnsafeIds(string? id)
    {
        PluginIdentifier.IsWellFormed(id).Should().BeFalse();
    }
}
