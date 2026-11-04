using System.Collections.Concurrent;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Lifecycle;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A plugin implementing <see cref="IPluginStatus"/> whose answer a test chooses through its own stored setting
/// <c>integration:{id}:behaviour</c>: <c>healthy</c>, <c>degraded</c>, <c>down</c>, <c>throw</c> or <c>hang</c> (ignores
/// its token). Each check is counted per plugin id, so a test can prove the host's cache.
/// </summary>
public sealed class ScriptedStatusPlugin : IPluginStatus
{
    /// <summary>The setting key, inside the plugin's section, that chooses the answer.</summary>
    public const string BehaviourField = "behaviour";

    /// <summary>The text a throwing check carries; it must never reach a response.</summary>
    public const string SecretFailureText = "status-secret-should-never-leak";

    /// <summary>The detail a healthy check reports: a key of the test manifest's locales.</summary>
    public const string HealthyDetailKey = "status.ok";

    /// <summary>Checks run per plugin id.</summary>
    private static readonly ConcurrentDictionary<string, int> Calls = new(StringComparer.Ordinal);

    /// <summary>The plugin id read from its configuration.</summary>
    private readonly string _id;

    /// <summary>The chosen answer.</summary>
    private readonly string _behaviour;

    /// <summary>Initializes a new instance of the <see cref="ScriptedStatusPlugin"/> class.</summary>
    /// <param name="configuration">The plugin's own configuration, as the host composes it.</param>
    public ScriptedStatusPlugin(IConfiguration configuration)
    {
        var entry = configuration.AsEnumerable()
            .FirstOrDefault(pair => pair.Key.EndsWith(":" + BehaviourField, StringComparison.Ordinal));
        _id = entry.Key?.Split(':')[1] ?? string.Empty;
        _behaviour = entry.Value ?? "healthy";
    }

    /// <summary>How many checks ran for a plugin.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The count.</returns>
    public static int CallsOf(string pluginId)
    {
        return Calls.TryGetValue(pluginId, out var count) ? count : 0;
    }

    /// <inheritdoc />
    public async Task<PluginStatusReport> GetStatusAsync(CancellationToken ct)
    {
        Calls.AddOrUpdate(_id, 1, (_, count) => count + 1);
        switch (_behaviour)
        {
            case "throw":
                throw new InvalidOperationException(SecretFailureText);
            case "hang":
                // Ignores the token on purpose: the host must stop waiting by itself.
                await Task.Delay(TimeSpan.FromSeconds(30), CancellationToken.None);
                return new PluginStatusReport(PluginHealth.Healthy);
            case "degraded":
                return new PluginStatusReport(PluginHealth.Degraded);
            case "down":
                return new PluginStatusReport(PluginHealth.Unavailable);
            default:
                return new PluginStatusReport(PluginHealth.Healthy, HealthyDetailKey);
        }
    }
}
