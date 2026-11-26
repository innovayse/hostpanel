using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Hosting.Extensions;

/// <summary>Registers how long the host lets running work finish when it stops.</summary>
public static class HostDrainExtensions
{
    /// <summary>
    /// Makes the host's shutdown timeout the drain time of <see cref="PluginOptions.DrainSeconds"/>. The web server stops taking
    /// connections when the host stops, and the host waits at most this long for the running requests and background jobs
    /// before it exits, so a restart never cuts off work that finishes within the drain time.
    /// </summary>
    /// <param name="services">The service collection; <see cref="PluginOptions"/> must be registered.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddHostDrain(this IServiceCollection services)
    {
        services.AddOptions<HostOptions>()
            .Configure<IOptions<PluginOptions>>((host, plugins) => host.ShutdownTimeout = TimeSpan.FromSeconds(plugins.Value.DrainSeconds));
        return services;
    }
}
