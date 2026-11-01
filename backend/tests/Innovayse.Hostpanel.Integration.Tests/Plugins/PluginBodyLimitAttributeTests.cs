using Innovayse.Hostpanel.API.Plugins;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// <see cref="PluginBodyLimitAttribute"/>: the web server's own body limit (about 28.6 MiB by default) is lifted on a plugin
/// route, so <c>PluginApi:MaxBodyBytes</c>, which may be larger, is the only limit and answers 413 <c>PLUGIN_REQUEST_TOO_LARGE</c>.
/// </summary>
public sealed class PluginBodyLimitAttributeTests
{
    /// <summary>The server's default limit, in bytes.</summary>
    private const long ServerDefault = 30_000_000;

    /// <summary>Runs the filter over a request whose server exposes the given feature.</summary>
    /// <param name="feature">The server's body limit feature, or <see langword="null"/> when the server has none.</param>
    private static void Run(IHttpMaxRequestBodySizeFeature? feature)
    {
        var context = new DefaultHttpContext();
        if (feature is not null)
        {
            context.Features.Set(feature);
        }

        var executing = new ResourceExecutingContext(
            new ActionContext(context, new RouteData(), new ActionDescriptor()), [], []);

        new PluginBodyLimitAttribute().OnResourceExecuting(executing);
    }

    /// <summary>The server's limit is removed, so the runner's cap decides.</summary>
    [Fact]
    public void TheServersLimit_IsLifted()
    {
        var feature = new FakeMaxRequestBodySizeFeature(ServerDefault);

        Run(feature);

        feature.MaxRequestBodySize.Should().BeNull();
    }

    /// <summary>A server that has begun reading the body cannot change its limit, and the filter does not try.</summary>
    [Fact]
    public void AReadOnlyLimit_IsLeftAlone()
    {
        var feature = new FakeMaxRequestBodySizeFeature(ServerDefault, isReadOnly: true);

        Run(feature);

        feature.MaxRequestBodySize.Should().Be(ServerDefault);
    }

    /// <summary>A server with no such feature, such as an in-memory one, is not an error.</summary>
    [Fact]
    public void ANoFeature_IsNotAnError()
    {
        var act = () => Run(null);

        act.Should().NotThrow();
    }
}
