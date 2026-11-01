using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Domain.Settings.Enums;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Manifest;

namespace Innovayse.Hostpanel.Application.Admin.Integrations.Extensions;

/// <summary>
/// Reads one setting a plugin manifest declares (<c>PluginManifest.GetSettings()</c>, the only reader of the
/// manifest's settings) into the host's two questions: is it secret, and what <see cref="FieldDefinitionDto"/>
/// does the admin form render for it. The plugin loader, the integration detail read and the secrecy classifier
/// share it, so a setting is read the same way wherever it is used.
/// </summary>
public static class PluginSettingMappingExtensions
{
    /// <summary>Tells whether a setting holds a secret.</summary>
    /// <param name="setting">The setting from <c>plugin.json</c>.</param>
    /// <returns>
    /// <c>true</c> for the secret kind and for a kind this release does not define: a manifest is third-party
    /// input, so an unknown kind fails closed and is never read as plain text.
    /// </returns>
    public static bool IsSecret(this PluginSetting setting)
    {
        ArgumentNullException.ThrowIfNull(setting);

        return setting.Type == PluginSettingType.Secret || !Enum.IsDefined(setting.Type);
    }

    /// <summary>Maps one manifest setting to the form definition the admin panel renders.</summary>
    /// <param name="setting">The setting from <c>plugin.json</c>.</param>
    /// <returns>
    /// The form definition. A secret (or unknown) kind maps to <see cref="IntegrationFieldType.Secret"/>, a
    /// boolean to a toggle, a textarea to a text area, a select to a select carrying the values of its options,
    /// and a text, url or number to plain text.
    /// </returns>
    public static FieldDefinitionDto ToFieldDefinition(this PluginSetting setting)
    {
        ArgumentNullException.ThrowIfNull(setting);

        if (setting.IsSecret())
        {
            return new FieldDefinitionDto(setting.Key, setting.Label, IntegrationFieldType.Secret, setting.Required);
        }

        var type = setting.Type switch
        {
            PluginSettingType.Select => IntegrationFieldType.Select,
            PluginSettingType.Textarea => IntegrationFieldType.TextArea,
            PluginSettingType.Boolean => IntegrationFieldType.Toggle,
            _ => IntegrationFieldType.Text,
        };

        IReadOnlyList<string>? options = setting.Type == PluginSettingType.Select && setting.Options.Count > 0
            ? setting.Options.Select(o => o.Value).ToList()
            : null;

        return new FieldDefinitionDto(setting.Key, setting.Label, type, setting.Required, options);
    }
}
