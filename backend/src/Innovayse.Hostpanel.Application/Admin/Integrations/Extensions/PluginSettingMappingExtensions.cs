using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Settings.Enums;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;

namespace Innovayse.Hostpanel.Application.Admin.Integrations.Extensions;

/// <summary>
/// Reads one setting a plugin manifest declares (<c>PluginManifest.GetSettings()</c>, the only reader of the
/// manifest's settings) into the host's two questions: is it secret, and what <see cref="FieldDefinitionDto"/>
/// does the admin form render for it. The integration detail read, the save and the secrecy classifier
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

    /// <summary>Maps one manifest setting to the form definition the admin panel renders, its texts in the reader's language.</summary>
    /// <param name="setting">The setting from <c>plugin.json</c>.</param>
    /// <param name="texts">
    /// The plugin's texts in the request's language: the label, help text, placeholder and option labels are keys of the
    /// manifest locales or plain text, resolved here (falling back to English) so the panel never shows a raw key.
    /// </param>
    /// <returns>
    /// The form definition. A secret (or unknown) kind maps to <see cref="IntegrationFieldType.Secret"/>, a
    /// boolean to a toggle, a textarea to a text area, a select to a select carrying the values of its options and
    /// their labels, a url to a url field, a number to a number field carrying its minimum and maximum, and a text to
    /// plain text.
    /// </returns>
    public static FieldDefinitionDto ToFieldDefinition(this PluginSetting setting, PluginUiTexts texts)
    {
        ArgumentNullException.ThrowIfNull(setting);

        var label = texts.Resolve(setting.Label);
        var description = setting.Description is null ? null : texts.Resolve(setting.Description);
        var placeholder = setting.Placeholder is null ? null : texts.Resolve(setting.Placeholder);

        if (setting.IsSecret())
        {
            return new FieldDefinitionDto(setting.Key, label, IntegrationFieldType.Secret, setting.Required,
                Description: description, Placeholder: placeholder);
        }

        var type = setting.Type switch
        {
            PluginSettingType.Select => IntegrationFieldType.Select,
            PluginSettingType.Textarea => IntegrationFieldType.TextArea,
            PluginSettingType.Boolean => IntegrationFieldType.Toggle,
            PluginSettingType.Url => IntegrationFieldType.Url,
            PluginSettingType.Number => IntegrationFieldType.Number,
            _ => IntegrationFieldType.Text,
        };

        var hasOptions = setting.Type == PluginSettingType.Select && setting.Options.Count > 0;
        IReadOnlyList<string>? options = hasOptions ? setting.Options.Select(o => o.Value).ToList() : null;

        // Keyed by value; a manifest that lists one value twice keeps the first label, as the select shows it once.
        IReadOnlyDictionary<string, string>? optionLabels = null;
        if (hasOptions)
        {
            var labels = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var option in setting.Options)
            {
                labels.TryAdd(option.Value, texts.Resolve(option.Label));
            }

            optionLabels = labels;
        }

        return new FieldDefinitionDto(setting.Key, label, type, setting.Required,
            options,
            setting.Type == PluginSettingType.Number ? setting.Min : null,
            setting.Type == PluginSettingType.Number ? setting.Max : null,
            description,
            placeholder,
            optionLabels);
    }
}
