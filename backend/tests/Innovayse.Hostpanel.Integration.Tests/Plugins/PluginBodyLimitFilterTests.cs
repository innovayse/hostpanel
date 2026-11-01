using Innovayse.Hostpanel.API.Plugins;
using Innovayse.Hostpanel.Application.Common.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// <see cref="PluginBodyLimitFilter"/>: the web server's own body limit (about 28.6 MiB by default) is replaced on a
/// plugin route by <c>PluginApi:MaxBodyBytes</c> plus one read chunk and the chunk framing, so the runner's cap answers first and an unread
/// body is still bounded. <see cref="NoFormBindingAttribute"/>: no form value provider is left to read the body.
/// </summary>
public sealed class PluginBodyLimitFilterTests
{
    /// <summary>The server's default limit, in bytes.</summary>
    private const long ServerDefault = 30_000_000;

    /// <summary>The cap the filter is configured with.</summary>
    private const int Cap = 1_000_000;

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

        new PluginBodyLimitFilter(Options.Create(new PluginApiOptions { MaxBodyBytes = Cap })).OnResourceExecuting(executing);
    }

    /// <summary>The server's limit becomes the cap plus one chunk and the chunk framing: above the cap, so the runner answers first, and finite.</summary>
    [Fact]
    public void TheServersLimit_BecomesTheCapPlusTheAllowance()
    {
        var feature = new FakeMaxRequestBodySizeFeature(ServerDefault);

        Run(feature);

        feature.MaxRequestBodySize.Should().Be(Cap + PluginApiOptions.ReadChunkBytes + (Cap / 64));
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

    /// <summary>Only the route and query value providers survive, so no provider can read a form.</summary>
    [Fact]
    public void OnlyTheRouteAndQueryValueProviders_Survive()
    {
        var factories = new List<IValueProviderFactory>
        {
            new FormValueProviderFactory(),
            new RouteValueProviderFactory(),
            new QueryStringValueProviderFactory(),
            new JQueryFormValueProviderFactory(),
            new FormFileValueProviderFactory(),
        };
        var executing = new ResourceExecutingContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()), [], factories);

        new NoFormBindingAttribute().OnResourceExecuting(executing);

        factories.Select(factory => factory.GetType()).Should().Equal(typeof(RouteValueProviderFactory), typeof(QueryStringValueProviderFactory));
    }
}
