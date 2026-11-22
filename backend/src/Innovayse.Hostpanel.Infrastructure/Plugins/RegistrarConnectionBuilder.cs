using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Registrar;
using Innovayse.Hostpanel.Domain.Settings;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Builds the <see cref="RegistrarConnection"/> a registrar plugin is called with from the settings the admin saved. The host
/// knows no setting key: the plugin's manifest names the setting that carries each part by its <c>connectionRole</c>.
/// </summary>
public static class RegistrarConnectionBuilder
{
    /// <summary>
    /// Reads the connection from the configuration <see cref="PluginActivator"/> composed for the plugin (secrets already
    /// revealed), so the connection holds exactly the values the plugin itself receives.
    /// </summary>
    /// <param name="manifest">The plugin's manifest, whose settings carry the roles.</param>
    /// <param name="configuration">The configuration composed for this plugin alone.</param>
    /// <returns>
    /// The connection; a part no setting plays, or one the admin left empty, is <see langword="null"/>, and the test mode is
    /// on only when its setting holds the enabled value. A manifest with no roles gives an empty connection and the plugin
    /// reads its own settings.
    /// </returns>
    public static RegistrarConnection Build(PluginManifest manifest, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(configuration);

        return new RegistrarConnection(
            Read(manifest, configuration, RegistrarConnectionRole.AccountName),
            Read(manifest, configuration, RegistrarConnectionRole.ApiKey),
            Read(manifest, configuration, RegistrarConnectionRole.ApiUrl),
            Read(manifest, configuration, RegistrarConnectionRole.ClientIp),
            string.Equals(
                Read(manifest, configuration, RegistrarConnectionRole.TestMode),
                IntegrationSettingKeys.EnabledValue,
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Reads the value of the setting that plays one role.</summary>
    /// <param name="manifest">The plugin's manifest.</param>
    /// <param name="configuration">The configuration composed for this plugin alone.</param>
    /// <param name="role">The role to read.</param>
    /// <returns>The trimmed value, or <see langword="null"/> when no setting plays the role or it holds nothing.</returns>
    private static string? Read(PluginManifest manifest, IConfiguration configuration, RegistrarConnectionRole role)
    {
        var setting = manifest.GetSettings().FirstOrDefault(s => s.ConnectionRole == role);
        if (setting is null)
        {
            return null;
        }

        var value = configuration[IntegrationSettingKeys.FieldKey(manifest.Id, setting.Key)]?.Trim();

        return string.IsNullOrEmpty(value) ? null : value;
    }
}
