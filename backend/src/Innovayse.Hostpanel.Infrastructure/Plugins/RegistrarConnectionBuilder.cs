using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Registrar;
using Innovayse.Hostpanel.Domain.Settings;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Builds the <see cref="RegistrarConnection"/> a registrar plugin is called with from the settings the admin saved. The host
/// knows no setting key: the plugin's manifest names the setting that carries each part by its <c>connectionRole</c>.
/// </summary>
public static class RegistrarConnectionBuilder
{
    /// <summary>
    /// Reads the connection from the settings <see cref="PluginActivator"/> composed for the plugin (secrets already
    /// revealed), so the connection holds exactly the values the plugin itself receives.
    /// </summary>
    /// <param name="manifest">The plugin's manifest, whose settings carry the roles.</param>
    /// <param name="settings">The settings composed for this plugin alone, keyed by their full path.</param>
    /// <returns>
    /// The connection; a part no setting plays, or one the admin left empty, is <see langword="null"/>, and the test mode is
    /// on only when its setting holds the enabled value. A manifest with no roles gives an empty connection and the plugin
    /// reads its own settings.
    /// </returns>
    public static RegistrarConnection Build(PluginManifest manifest, IReadOnlyDictionary<string, string?> settings)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(settings);

        return new RegistrarConnection(
            Read(manifest, settings, RegistrarConnectionRole.AccountName),
            Read(manifest, settings, RegistrarConnectionRole.ApiKey),
            Read(manifest, settings, RegistrarConnectionRole.ApiUrl),
            Read(manifest, settings, RegistrarConnectionRole.ClientIp),
            string.Equals(
                Read(manifest, settings, RegistrarConnectionRole.TestMode),
                IntegrationSettingKeys.EnabledValue,
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Reads the value of the setting that plays one role.</summary>
    /// <param name="manifest">The plugin's manifest.</param>
    /// <param name="settings">The settings composed for this plugin alone, keyed by their full path.</param>
    /// <param name="role">The role to read.</param>
    /// <returns>The trimmed value, or <see langword="null"/> when no setting plays the role or it holds nothing.</returns>
    private static string? Read(PluginManifest manifest, IReadOnlyDictionary<string, string?> settings, RegistrarConnectionRole role)
    {
        var setting = manifest.GetSettings().FirstOrDefault(s => s.ConnectionRole == role);
        if (setting is null)
        {
            return null;
        }

        var value = settings.GetValueOrDefault(IntegrationSettingKeys.FieldKey(manifest.Id, setting.Key))?.Trim();

        return string.IsNullOrEmpty(value) ? null : value;
    }
}
