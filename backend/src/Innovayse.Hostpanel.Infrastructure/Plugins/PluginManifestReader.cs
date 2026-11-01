using System.Text.Json;
using System.Text.Json.Serialization;
using Innovayse.SDK.Plugins;
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
    /// The text is not a well-formed manifest, or a <c>settings</c> entry has a <c>type</c> this release does not
    /// define (a number outside the kinds, which the string converter would otherwise accept and read as plain
    /// text). A manifest is refused rather than loaded with a setting whose secrecy is unknown.
    /// </exception>
    internal static PluginManifest? Deserialize(string json)
    {
        var manifest = JsonSerializer.Deserialize<PluginManifest>(json, JsonOptions);
        var unknown = manifest?.Settings.FirstOrDefault(s => !Enum.IsDefined(s.Type));
        if (unknown is not null)
        {
            throw new JsonException($"Setting '{unknown.Key}' has an unknown type.");
        }

        return manifest;
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
