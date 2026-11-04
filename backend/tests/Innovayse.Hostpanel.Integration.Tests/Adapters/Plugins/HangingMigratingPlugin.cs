using Innovayse.SDK.Plugins.Lifecycle;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// A plugin whose migration hook never finishes and never looks at its cancellation token, to prove the host stops
/// waiting for it after the time limit and keeps the upgrade pending.
/// </summary>
internal sealed class HangingMigratingPlugin : IPluginMigrations
{
    /// <summary>Initializes the plugin.</summary>
    /// <param name="configuration">The plugin's own configuration, which the activator hands every plugin; unused here.</param>
    public HangingMigratingPlugin(IConfiguration configuration)
    {
    }

    /// <inheritdoc/>
    public Task MigrateAsync(CancellationToken ct)
    {
        return new TaskCompletionSource().Task;
    }
}
