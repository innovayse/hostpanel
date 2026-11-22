using Hostpanel.Sdk.Plugins.Endpoints;
using Hostpanel.Sdk.Plugins.Provisioning;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>A provisioning plugin that also implements the endpoints contract, the shape the <c>endpoints</c> capability promises (#1357).</summary>
internal sealed class ProvisioningEndpointsPlugin : IProvisioningPlugin, IPluginEndpoints
{
    /// <summary>Initializes the plugin.</summary>
    /// <param name="configuration">The plugin's own configuration, which the activator hands every plugin; unused here.</param>
    public ProvisioningEndpointsPlugin(IConfiguration configuration)
    {
    }

    /// <inheritdoc/>
    public Task<PluginReply> HandleAsync(PluginCall call, CancellationToken ct)
    {
        return ServingPluginReplies.AnswerAsync(call, ct);
    }
}
