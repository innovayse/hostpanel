using Hostpanel.Sdk.Plugins.Lifecycle;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>A plugin whose migration hook always throws, to prove the host keeps the upgrade pending and carries on.</summary>
internal sealed class FailingMigratingPlugin : IPluginMigrations
{
    /// <summary>Initializes the plugin.</summary>
    /// <param name="configuration">The plugin's own configuration, which the activator hands every plugin; unused here.</param>
    public FailingMigratingPlugin(IConfiguration configuration)
    {
    }

    /// <inheritdoc/>
    public Task MigrateAsync(CancellationToken ct)
    {
        throw new InvalidOperationException("The plugin's own data could not be migrated.");
    }
}
