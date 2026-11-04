using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.SDK.Plugins.Endpoints;
using Microsoft.Extensions.DependencyInjection;

/*
 * Everything a streamed call hands the plugin is resolved from this one scope: the endpoint resolver (so the plugin's
 * activator, and the plugin it constructs, take their dependencies from here) and the call services (so the store and the
 * host API write through this scope's AppDbContext). Resolving from the scope's provider is the factory exception to "no
 * service location": the scope is created at run time and the container is the only thing that can build its services.
 */

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary><see cref="IPluginCallScope"/> over one container scope, disposed with it.</summary>
internal sealed class PluginCallScope : IPluginCallScope
{
    /// <summary>The container scope every service of the call is resolved from.</summary>
    private readonly AsyncServiceScope _scope;

    /// <summary>Initializes the scope.</summary>
    /// <param name="scope">The container scope every service of the call is resolved from; owned from now on.</param>
    public PluginCallScope(AsyncServiceScope scope)
    {
        _scope = scope;
    }

    /// <inheritdoc/>
    public Task<Func<IPluginEndpoints>?> PrepareEndpointsAsync(string pluginId, CancellationToken ct)
    {
        return _scope.ServiceProvider.GetRequiredService<IPluginEndpointResolver>().PrepareAsync(pluginId, ct);
    }

    /// <inheritdoc/>
    public PluginCallServices CreateServices(PluginCallBinding binding)
    {
        return _scope.ServiceProvider.GetRequiredService<IPluginCallServices>().Create(binding);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        return _scope.DisposeAsync();
    }
}
