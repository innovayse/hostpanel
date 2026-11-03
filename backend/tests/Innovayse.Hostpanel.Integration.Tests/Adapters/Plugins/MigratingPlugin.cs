using System.Collections.Concurrent;
using Innovayse.SDK.Plugins.Lifecycle;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// A plugin with a migration hook that counts how often the host calls it. The count is static because the host
/// builds the plugin itself, from the plugin's constructor, and a test cannot hand it a journal; it is keyed by plugin
/// id, which every test makes unique.
/// </summary>
internal sealed class MigratingPlugin : IPluginMigrations
{
    /// <summary>How many times the hook ran, by plugin id.</summary>
    private static readonly ConcurrentDictionary<string, int> Calls = new();

    /// <summary>The id of the plugin this instance was built for, read from its own configuration.</summary>
    private readonly string _id;

    /// <summary>Initializes the plugin.</summary>
    /// <param name="configuration">The plugin's own configuration, whose keys are <c>integration:{id}:{key}</c>.</param>
    public MigratingPlugin(IConfiguration configuration)
    {
        _id = configuration.AsEnumerable().First(pair => pair.Key.StartsWith("integration:", StringComparison.Ordinal)).Key.Split(':')[1];
    }

    /// <summary>How many times the hook ran for one plugin.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The call count; zero when the hook never ran.</returns>
    public static int CallsOf(string pluginId)
    {
        return Calls.GetValueOrDefault(pluginId);
    }

    /// <inheritdoc/>
    public Task MigrateAsync(CancellationToken ct)
    {
        Calls.AddOrUpdate(_id, 1, (_, count) => count + 1);
        return Task.CompletedTask;
    }
}
