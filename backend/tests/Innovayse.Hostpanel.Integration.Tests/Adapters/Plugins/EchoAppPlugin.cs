using Innovayse.SDK.Plugins.Endpoints;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>A minimal app plugin: answers every call with no content.</summary>
internal sealed class EchoAppPlugin : IPluginEndpoints
{
    /// <summary>Initializes the plugin.</summary>
    /// <param name="configuration">The plugin's own configuration, which the activator hands every plugin; unused here.</param>
    public EchoAppPlugin(IConfiguration configuration)
    {
    }

    /// <inheritdoc/>
    public Task<PluginReply> HandleAsync(PluginCall call, CancellationToken ct)
    {
        return Task.FromResult(PluginReply.NoContent());
    }
}
