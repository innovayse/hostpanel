using Innovayse.Hostpanel.Application.Plugins.Common;
using Hostpanel.Sdk.Plugins.Manifest;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// <see cref="PluginPagePathMatcher"/>: a requested path is split only when it has a page path's shape, a parameter
/// takes one segment, literals compare ignoring case, a literal beats a parameter and the first of two equal templates
/// wins.
/// </summary>
public sealed class PluginPagePathMatcherTests
{
    /// <summary>The pages every test matches against, in manifest order.</summary>
    private static readonly IReadOnlyList<PluginPage> Pages =
    [
        Page("sites", "/sites"),
        Page("site", "/sites/{siteId}"),
        Page("new-site", "/sites/new"),
        Page("site-again", "/sites/{id}"),
    ];

    /// <summary>A declared page.</summary>
    /// <param name="id">The page id.</param>
    /// <param name="path">The page path template.</param>
    /// <returns>The page.</returns>
    private static PluginPage Page(string id, string path)
    {
        return new PluginPage { Id = id, Path = path, Title = id, View = $"views/{id}.json" };
    }

    /// <summary>Finds the page a path names, or null when the path is malformed or names none.</summary>
    /// <param name="path">The requested path.</param>
    /// <returns>The page id, or <see langword="null"/>.</returns>
    private static string? Find(string? path)
    {
        return PluginPagePathMatcher.Segments(path) is { } segments
            ? PluginPagePathMatcher.Match(Pages, segments)?.Id
            : null;
    }

    /// <summary>A literal path finds its page, in any case.</summary>
    /// <param name="path">The requested path.</param>
    [Theory]
    [InlineData("/sites")]
    [InlineData("/SITES")]
    [InlineData("/Sites")]
    public void Match_LiteralPath_IgnoringCase(string path)
    {
        Find(path).Should().Be("sites");
    }

    /// <summary>A parameter takes any one segment, and the first page of two with the same template wins.</summary>
    [Fact]
    public void Match_ParameterTakesOneSegment_FirstDeclaredWins()
    {
        Find("/sites/42").Should().Be("site");
        Find("/sites/a%2Fb").Should().Be("site");
    }

    /// <summary>A literal beats a parameter at the same position.</summary>
    [Fact]
    public void Match_LiteralBeatsParameter()
    {
        Find("/sites/new").Should().Be("new-site");
        Find("/sites/NEW").Should().Be("new-site");
    }

    /// <summary>A path of another segment count names no page.</summary>
    [Fact]
    public void Match_OtherShape_FindsNothing()
    {
        Find("/sites/42/edit").Should().BeNull();
        Find("/other").Should().BeNull();
    }

    /// <summary>A path without a page path's shape is not split at all.</summary>
    /// <param name="path">The malformed path.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("sites")]
    [InlineData("/sites/")]
    [InlineData("//sites")]
    [InlineData("/sites/./x")]
    [InlineData("/sites/..")]
    [InlineData("/sites\\42")]
    [InlineData("/sites/4\t2")]
    [InlineData("/sites\r\n")]
    public void Segments_MalformedPath_IsNull(string? path)
    {
        PluginPagePathMatcher.Segments(path).Should().BeNull();
    }

    /// <summary>A path of exactly the length bound is accepted and one character more is refused.</summary>
    [Fact]
    public void Segments_LengthBound()
    {
        var atBound = "/" + new string('a', PluginPagePathMatcher.MaxPathLength - 1);

        PluginPagePathMatcher.Segments(atBound).Should().NotBeNull();
        PluginPagePathMatcher.Segments(atBound + "a").Should().BeNull();
    }
}
