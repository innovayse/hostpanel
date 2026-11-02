using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Manifest;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// <see cref="PluginRouteMatcher"/>: a request matches the declared route with the same method and shape, parameters
/// bind to the segments they stand for, and a literal beats a parameter.
/// </summary>
public sealed class PluginRouteMatcherTests
{
    /// <summary>The routes every test matches against.</summary>
    private static readonly IReadOnlyList<PluginRoute> Routes =
    [
        new() { Method = PluginRouteMethod.Get, Path = "/invoices/{id}" },
        new() { Method = PluginRouteMethod.Post, Path = "/invoices/{id}/pay" },
        new() { Method = PluginRouteMethod.Get, Path = "/items/{name}" },
        new() { Method = PluginRouteMethod.Get, Path = "/items/latest" },
        new() { Method = PluginRouteMethod.Get, Path = "/a/{x}/{y}" },
        new() { Method = PluginRouteMethod.Get, Path = "/a/{x}/fixed" },
    ];

    /// <summary>The route values are the segments the parameters stand for, by the declared names.</summary>
    [Fact]
    public void Match_BindsParametersByName()
    {
        var match = PluginRouteMatcher.Match(Routes, PluginRouteMethod.Post, "invoices/17/pay");

        match.Should().NotBeNull();
        match!.Route.Path.Should().Be("/invoices/{id}/pay");
        match.Values.Should().Equal(new Dictionary<string, string> { ["id"] = "17" });
    }

    /// <summary>The same path with another method is not the route.</summary>
    [Fact]
    public void Match_AnotherMethod_MatchesNothing()
    {
        PluginRouteMatcher.Match(Routes, PluginRouteMethod.Delete, "invoices/17").Should().BeNull();
        PluginRouteMatcher.Match(Routes, PluginRouteMethod.Get, "invoices/17/pay").Should().BeNull();
    }

    /// <summary>A literal segment wins over a parameter in the same place, whatever order they were declared in.</summary>
    [Fact]
    public void Match_ALiteralBeatsAParameter()
    {
        var literal = PluginRouteMatcher.Match(Routes, PluginRouteMethod.Get, "items/latest");
        var parameter = PluginRouteMatcher.Match(Routes, PluginRouteMethod.Get, "items/older");
        var deeper = PluginRouteMatcher.Match(Routes, PluginRouteMethod.Get, "a/1/fixed");

        literal!.Route.Path.Should().Be("/items/latest");
        literal.Values.Should().BeEmpty();
        parameter!.Route.Path.Should().Be("/items/{name}");
        deeper!.Route.Path.Should().Be("/a/{x}/fixed");
        deeper.Values.Should().Equal(new Dictionary<string, string> { ["x"] = "1" });
    }

    /// <summary>Literals compare without regard to case, as the rest of the API's routes do; parameter values keep their case.</summary>
    [Fact]
    public void Match_IgnoresTheCaseOfLiterals_AndKeepsTheCaseOfValues()
    {
        var match = PluginRouteMatcher.Match(Routes, PluginRouteMethod.Get, "INVOICES/AbC");

        match!.Route.Path.Should().Be("/invoices/{id}");
        match.Values["id"].Should().Be("AbC");
    }

    /// <summary>An empty path, an empty segment, a trailing slash and a wrong segment count match nothing.</summary>
    /// <param name="path">A path no route serves.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invoices")]
    [InlineData("invoices/")]
    [InlineData("invoices//pay")]
    [InlineData("invoices/1/2/3")]
    [InlineData("nothing")]
    public void Match_UnservedPaths_MatchNothing(string? path)
    {
        PluginRouteMatcher.Match(Routes, PluginRouteMethod.Get, path).Should().BeNull();
        PluginRouteMatcher.Match(Routes, PluginRouteMethod.Post, path).Should().BeNull();
    }
}
