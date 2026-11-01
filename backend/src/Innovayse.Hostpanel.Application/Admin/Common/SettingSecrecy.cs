using Innovayse.Hostpanel.Application.Admin.Integrations.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.SDK.Plugins.Manifest;

namespace Innovayse.Hostpanel.Application.Admin.Common;

/// <summary>
/// Classifies setting keys as secret for every settings read and write: the keys
/// <see cref="SecretSettingKeys"/> names, plus every setting a loaded plugin's manifest declares with
/// the secret kind or a kind this release does not define. The manifest's settings and its legacy fields are
/// both read, so a manifest that mixes the two keeps a secret field secret even when its settings omit it.
/// It is the one question every handler asks, so no handler decides on its own.
/// </summary>
public sealed class SettingSecrecy
{
    /// <summary>Full setting keys of the plugin settings declared as secret, compared case-insensitively.</summary>
    private readonly HashSet<string> _pluginSecretKeys;

    /// <summary>Initializes the classifier over the plugin-declared secret keys.</summary>
    /// <param name="pluginSecretKeys">Full setting keys of the plugin settings declared as secret.</param>
    private SettingSecrecy(HashSet<string> pluginSecretKeys)
    {
        _pluginSecretKeys = pluginSecretKeys;
    }

    /// <summary>Builds the classifier from the plugins loaded at startup.</summary>
    /// <param name="plugins">The registry of loaded plugins.</param>
    /// <returns>The classifier.</returns>
    public static SettingSecrecy From(IPluginRegistry plugins)
    {
        ArgumentNullException.ThrowIfNull(plugins);

        // GetSettings() hides the legacy fields once a manifest lists settings. Secrecy must not depend on
        // which list a secret was declared in, so the legacy fields are read as settings too and the union wins.
        var keys = plugins.GetLoadedManifests()
            .SelectMany(m => m.GetSettings()
                .Concat(m.Fields.Select(PluginSetting.FromField))
                .Where(s => s.IsSecret())
                .Select(s => IntegrationSettingKeys.FieldKey(m.Id, s.Key)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new SettingSecrecy(keys);
    }

    /// <summary>Tells whether a full setting key holds a secret.</summary>
    /// <param name="key">The full setting key.</param>
    /// <returns><c>true</c> when the value must never be returned by a read.</returns>
    public bool IsSecret(string key)
    {
        return SecretSettingKeys.IsSecret(key) || _pluginSecretKeys.Contains(key);
    }
}
