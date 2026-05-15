namespace Innovayse.Infrastructure.Plugins;
using System.Text.Json;
using System.Text.Json.Serialization;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Logging;

/// <summary>
/// Reads and validates <c>plugin.json</c> manifest files from the plugins directory.
/// </summary>
internal sealed class PluginManifestReader(ILogger<PluginManifestReader> logger)
{
    /// <summary>Supported SDK version. Plugins with a different version are skipped.</summary>
    private const string SupportedSdkVersion = "1.0";

    /// <summary>JSON options used to deserialise manifests.</summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

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
            logger.LogWarning("Plugin manifest not found: {Path}", manifestPath);
            return null;
        }

        try
        {
            var json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<PluginManifest>(json, _jsonOptions);

            if (manifest is null)
            {
                logger.LogWarning("Plugin manifest deserialised as null: {Path}", manifestPath);
                return null;
            }

            if (manifest.SdkVersion != SupportedSdkVersion)
            {
                logger.LogWarning(
                    "Plugin {Id} requires SDK version {Required}, supported is {Supported} — skipping",
                    manifest.Id, manifest.SdkVersion, SupportedSdkVersion);
                return null;
            }

            return manifest;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read plugin manifest: {Path}", manifestPath);
            return null;
        }
    }
}
