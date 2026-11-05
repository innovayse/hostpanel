using Hostpanel.Sdk.Packaging.Naming;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>The plugin page id rule: plain lowercase segments pass, anything a URL or a path could read as structure fails.</summary>
public sealed class PluginPageIdentifierTests
{
    /// <summary>Lowercase letters, digits, hyphens and underscores, starting with a letter or digit, are accepted.</summary>
    /// <param name="id">The candidate id.</param>
    [Theory]
    [InlineData("reports")]
    [InlineData("a")]
    [InlineData("0")]
    [InlineData("monthly-report_2")]
    [InlineData("a--b")]
    [InlineData("a__b")]
    [InlineData("trailing-")]
    [InlineData("trailing_")]
    public void IsValid_PlainIds_AreAccepted(string id)
    {
        PluginPageIdentifier.IsValid(id).Should().BeTrue();
    }

    /// <summary>Empty values, uppercase, separators, dots, a leading hyphen or underscore and control characters are refused.</summary>
    /// <param name="id">The candidate id.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Reports")]
    [InlineData("REPORTS")]
    [InlineData("-leading")]
    [InlineData("_leading")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a.b")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("with space")]
    [InlineData("a%2Fb")]
    [InlineData("é")]
    [InlineData("reports\n")]
    [InlineData("\nreports")]
    [InlineData("rep\0orts")]
    public void IsValid_UnsafeIds_AreRefused(string? id)
    {
        PluginPageIdentifier.IsValid(id).Should().BeFalse();
    }

    /// <summary>An id of exactly <see cref="PluginPageIdentifier.MaxLength"/> characters passes and one more is refused.</summary>
    [Fact]
    public void IsValid_OverlongId_IsRefused()
    {
        PluginPageIdentifier.IsValid(new string('a', PluginPageIdentifier.MaxLength)).Should().BeTrue();
        PluginPageIdentifier.IsValid(new string('a', PluginPageIdentifier.MaxLength + 1)).Should().BeFalse();
    }

    /// <summary>The page id bound is the plugin id bound, so both segments of a page address share one limit.</summary>
    [Fact]
    public void MaxLength_IsThePluginIdBound()
    {
        PluginPageIdentifier.MaxLength.Should().Be(PluginIdentifier.MaxLength);
    }

    /// <summary>The id the host uses for the page lookup by path is refused, so no page is shadowed by that route.</summary>
    [Fact]
    public void ByPath_IsReserved()
    {
        PluginPageIdentifier.IsValid("by-path").Should().BeFalse();
        PluginPageIdentifier.IsValid("by-path-2").Should().BeTrue();
    }
}
