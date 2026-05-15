namespace Innovayse.Application.Admin.Plugins.Queries.ListPlugins;
using System.Text.Json;
using System.Text.Json.Serialization;
using Innovayse.Application.Admin.Plugins.DTOs;
using Innovayse.Application.Admin.Plugins.Interfaces;
using Innovayse.SDK.Plugins;

/// <summary>
/// Handles <see cref="ListPluginsQuery"/> by scanning the plugins directory and
/// cross-referencing loaded status from <see cref="IPluginRegistry"/>.
/// </summary>
/// <param name="registry">Registry of plugins loaded at startup.</param>
public sealed class ListPluginsHandler(IPluginRegistry registry)
{
    /// <summary>JSON options for deserialising plugin manifests.</summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>
    /// Scans the plugins directory and returns a summary list.
    /// </summary>
    /// <param name="query">The list query (no parameters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All installed plugins, loaded or not.</returns>
    public async Task<IReadOnlyList<PluginListItemDto>> HandleAsync(ListPluginsQuery query, CancellationToken ct)
    {
        var pluginsRoot = Path.Combine(AppContext.BaseDirectory, "plugins");
        var result = new List<PluginListItemDto>();

        if (!Directory.Exists(pluginsRoot))
        {
            return result;
        }

        foreach (var dir in Directory.GetDirectories(pluginsRoot))
        {
            var manifestPath = Path.Combine(dir, "plugin.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                var json = await File.ReadAllTextAsync(manifestPath, ct);
                var manifest = JsonSerializer.Deserialize<PluginManifest>(json, _jsonOptions);
                if (manifest is null)
                {
                    continue;
                }

                result.Add(new PluginListItemDto(
                    manifest.Id,
                    manifest.Name,
                    manifest.Version,
                    manifest.Author,
                    manifest.Description,
                    manifest.Type.ToString().ToLowerInvariant(),
                    manifest.Category,
                    manifest.Color,
                    registry.IsLoaded(manifest.Id)));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Malformed manifest — skip silently (logged at startup by PluginLoader)
            }
        }

        return result;
    }
}
