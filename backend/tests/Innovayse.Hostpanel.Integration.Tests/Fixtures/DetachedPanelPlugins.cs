using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// The panel plugin parts of an adapter under test that exercises only the host's own panel providers: an empty registry, so no
/// module is served by a plugin and nothing is ever activated. The activator behind the caller therefore has no database and no
/// cipher, which it would only touch for a plugin; the tests that call a plugin build theirs over the real container instead.
/// </summary>
internal static class DetachedPanelPlugins
{
    /// <summary>Builds the empty registry, the resolver over it, the activator and the caller.</summary>
    /// <returns>The four parts, all built over the one empty registry.</returns>
    public static (PluginRegistry Registry, ServerModulePluginResolver Resolver, PluginActivator Activator, PanelPluginCaller Caller) Build()
    {
        var registry = new PluginRegistry();
        var activator = new PluginActivator(
            null!,
            null!,
            new ServiceCollection().BuildServiceProvider(),
            new ConfigurationBuilder().Build(),
            registry,
            new PluginUsageLeases(),
            NullLogger<PluginActivator>.Instance);

        return (
            registry,
            new ServerModulePluginResolver(registry, NullLogger<ServerModulePluginResolver>.Instance),
            activator,
            new PanelPluginCaller(registry, activator, Options.Create(new PluginApiOptions()), NullLogger<PanelPluginCaller>.Instance));
    }
}
