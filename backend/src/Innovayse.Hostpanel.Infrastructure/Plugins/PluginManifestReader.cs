using System.Text.Json;
using System.Text.Json.Serialization;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Manifest;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Reads and validates <c>plugin.json</c> manifest files from the plugins directory.
/// </summary>
internal sealed class PluginManifestReader
{
    /// <summary>Supported SDK version. Plugins with a different version are skipped at startup and refused on upload.</summary>
    internal const string SupportedSdkVersion = "1.0";

    /// <summary>JSON options used to deserialise manifests.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Records a manifest that is refused and why.</summary>
    private readonly ILogger<PluginManifestReader> _logger;

    /// <summary>Initializes a new instance of the <see cref="PluginManifestReader"/> class.</summary>
    /// <param name="logger">Records a manifest that is refused and why.</param>
    public PluginManifestReader(ILogger<PluginManifestReader> logger)
    {
        _logger = logger;
    }

    /// <summary>Parses the text of a <c>plugin.json</c> file, without checking the SDK version.</summary>
    /// <param name="json">The manifest text.</param>
    /// <returns>The manifest, or <see langword="null"/> when the text is the JSON literal <c>null</c>.</returns>
    /// <exception cref="JsonException">
    /// The text is not a well-formed manifest; a <c>settings</c> entry is <c>null</c> or has a <c>type</c> this release
    /// does not define (a number outside the kinds, which the string converter would otherwise accept and read as
    /// plain text); or a legacy <c>fields</c> entry is <c>null</c> or has a <c>type</c> the SDK does not recognise
    /// (it would be read as plain text). A manifest is refused rather than loaded with a setting whose secrecy is
    /// unknown. A <c>null</c> or missing <c>settings</c> or <c>fields</c> list is not an error: the SDK reads it as empty.
    /// </exception>
    internal static PluginManifest? Deserialize(string json)
    {
        var manifest = JsonSerializer.Deserialize<PluginManifest>(json, JsonOptions);
        if (manifest is null)
        {
            return null;
        }

        foreach (var setting in manifest.Settings)
        {
            if (setting is null || !Enum.IsDefined(setting.Type))
            {
                throw new JsonException($"Setting '{setting?.Key}' has an unknown type.");
            }
        }

        foreach (var field in manifest.Fields)
        {
            if (field is null || !IsKnownFieldType(field))
            {
                throw new JsonException($"Field '{field?.Key}' has an unknown type.");
            }
        }

        return manifest;
    }

    /// <summary>Tells whether the SDK recognises the type of a legacy field.</summary>
    /// <param name="field">The legacy field.</param>
    /// <returns>
    /// <see langword="true"/> unless the SDK would read the type as plain text only because it does not know it.
    /// <c>text</c> is the one type that really means plain text; asking the SDK keeps this list out of the host.
    /// </returns>
    private static bool IsKnownFieldType(PluginField field)
    {
        return string.Equals(field.Type, "text", StringComparison.OrdinalIgnoreCase)
            || PluginSetting.FromField(field).Type != PluginSettingType.Text;
    }

    /// <summary>
    /// Attempts to read and validate a <c>plugin.json</c> file.
    /// </summary>
    /// <param name="manifestPath">Absolute path to the <c>plugin.json</c> file.</param>
    /// <returns>
    /// A <see cref="PluginManifest"/> when the file is valid;
    /// <see langword="null"/> when the file is missing, malformed, or incompatible.
    /// </returns>
    public PluginManifest? TryRead(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            _logger.LogWarning("Plugin manifest not found: {Path}", manifestPath);
            return null;
        }

        try
        {
            var json = File.ReadAllText(manifestPath);
            var manifest = Deserialize(json);

            if (manifest is null)
            {
                _logger.LogWarning("Plugin manifest deserialised as null: {Path}", manifestPath);
                return null;
            }

            if (manifest.SdkVersion != SupportedSdkVersion)
            {
                _logger.LogWarning(
                    "Plugin {Id} requires SDK version {Required}, supported is {Supported} — skipping",
                    manifest.Id, manifest.SdkVersion, SupportedSdkVersion);
                return null;
            }

            return manifest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read plugin manifest: {Path}", manifestPath);
            return null;
        }
    }
}
