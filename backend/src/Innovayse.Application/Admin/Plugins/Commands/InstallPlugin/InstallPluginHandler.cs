namespace Innovayse.Application.Admin.Plugins.Commands.InstallPlugin;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Innovayse.Application.Admin.Plugins.DTOs;
using Innovayse.SDK.Plugins;

/// <summary>
/// Handles <see cref="InstallPluginCommand"/> — extracts and validates the ZIP,
/// writes plugin files to the plugins directory, and returns a restart-required result.
/// </summary>
public sealed class InstallPluginHandler
{
    /// <summary>Supported SDK version. Plugins with a different version are rejected.</summary>
    private const string SupportedSdkVersion = "1.0";

    /// <summary>JSON options for deserialising plugin manifests.</summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>
    /// Extracts the ZIP, validates the manifest, and writes plugin files to disk.
    /// </summary>
    /// <param name="command">Command containing the raw ZIP bytes.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Action result with <c>RequiresRestart = true</c>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>plugin.json</c> is missing, malformed, or incompatible.
    /// </exception>
    public async Task<PluginActionResultDto> HandleAsync(InstallPluginCommand command, CancellationToken ct)
    {
        using var zipStream = new MemoryStream(command.ZipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var manifestEntry = archive.GetEntry("plugin.json")
            ?? throw new InvalidOperationException("plugin.json not found in ZIP root.");

        string json;
        using (var reader = new StreamReader(manifestEntry.Open()))
        {
            json = await reader.ReadToEndAsync(ct);
        }

        var manifest = JsonSerializer.Deserialize<PluginManifest>(json, _jsonOptions)
            ?? throw new InvalidOperationException("plugin.json could not be parsed.");

        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            throw new InvalidOperationException("Plugin manifest is missing required field: id.");
        }

        if (manifest.SdkVersion != SupportedSdkVersion)
        {
            throw new InvalidOperationException(
                $"Plugin requires SDK version '{manifest.SdkVersion}', supported is '{SupportedSdkVersion}'.");
        }

        var pluginsRoot = Path.Combine(AppContext.BaseDirectory, "plugins");
        var targetDir = Path.Combine(pluginsRoot, manifest.Id);
        Directory.CreateDirectory(targetDir);

        var resolvedTargetDir = Path.GetFullPath(targetDir) + Path.DirectorySeparatorChar;

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue; // directory entry — skip
            }

            var relativePath = entry.FullName.Replace('\\', '/');
            var targetPath = Path.GetFullPath(Path.Combine(targetDir, relativePath));

            // Guard against zip-slip path traversal
            if (!targetPath.StartsWith(resolvedTargetDir, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Zip entry '{entry.FullName}' would escape the plugin directory.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            await using var entryStream = entry.Open();
            await using var fileStream = File.Create(targetPath);
            await entryStream.CopyToAsync(fileStream, ct);
        }

        return new PluginActionResultDto(RequiresRestart: true);
    }
}
