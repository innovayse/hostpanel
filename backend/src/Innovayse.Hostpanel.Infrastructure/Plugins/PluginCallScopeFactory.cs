using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

/*
 * A streamed plugin reply is enumerated by the response after the use case returned, and Wolverine disposes the use case's
 * scope, and with it the data context the plugin's store and host API were built on, as soon as the handler returns. That
 * left every streamed call that touched its store reading a disposed AppDbContext (#851). The scope opened here comes from
 * the root container, so nothing but its holder ends it.
 */

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary><see cref="IPluginCallScopeFactory"/> over the container's own scope factory.</summary>
public sealed class PluginCallScopeFactory : IPluginCallScopeFactory
{
    /// <summary>Creates scopes from the root container, independent of the request's.</summary>
    private readonly IServiceScopeFactory _scopes;

    /// <summary>Initializes the factory.</summary>
    /// <param name="scopes">Creates scopes from the root container, independent of the request's.</param>
    public PluginCallScopeFactory(IServiceScopeFactory scopes)
    {
        _scopes = scopes;
    }

    /// <inheritdoc/>
    public IPluginCallScope Open()
    {
        return new PluginCallScope(_scopes.CreateAsyncScope());
    }
}
