using System.Text.Json;
using System.Text.Json.Serialization;
using Hostpanel.Sdk.Packaging.Naming;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Keeps, per plugin, how the last change to it ended, in a small file under the upload folder. It is a file and not a
/// table because the change is decided while the host starts, before the database is needed, and the record has to survive
/// the restart that applied the change.
/// </summary>
/// <remarks>
/// A record is written once the host has tried to load the new version, by the startup loader and by an in place reload.
/// A change a restart applied is <i>unreported</i> until <see cref="PluginChangeReporter"/> has written it to the activity log,
/// which needs the database; a change an admin's own request applied is reported by that request.
/// </remarks>
public sealed class PluginChangeStore : IPluginChanges
{
    /// <summary>JSON options of the record files; enums are written by name so a file reads on its own.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The upload folder the record files live under.</summary>
    private readonly string _installRoot;

    /// <summary>Records a record that could not be read or written.</summary>
    private readonly ILogger<PluginChangeStore> _logger;

    /// <summary>Initializes the store.</summary>
    /// <param name="installRoot">The upload folder the record files live under.</param>
    /// <param name="logger">Records a record that could not be read or written.</param>
    public PluginChangeStore(string installRoot, ILogger<PluginChangeStore> logger)
    {
        _installRoot = installRoot;
        _logger = logger;
    }

    /// <inheritdoc/>
    public PluginChangeDto? Find(string pluginId)
    {
        return PluginIdentifier.IsWellFormed(pluginId) ? Read(pluginId)?.Change : null;
    }

    /// <summary>Writes how a change ended, replacing the record of the plugin's earlier change.</summary>
    /// <param name="change">The change and how it ended.</param>
    /// <param name="reported">Whether the activity log already has a line for it.</param>
    public void Record(PluginChangeDto change, bool reported)
    {
        try
        {
            var folder = Path.Combine(_installRoot, PluginFolders.Changes);
            Directory.CreateDirectory(folder);
            File.WriteAllText(FileOf(change.PluginId), JsonSerializer.Serialize(new PluginChangeEntry(change, reported), JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The change itself stands; only its record is lost, which the log says.
            _logger.LogError(ex, "Could not record the change of plugin {Id}", change.PluginId);
        }
    }

    /// <summary>Reads the changes the activity log has no line for yet.</summary>
    /// <returns>The unreported changes, in plugin id order; empty when none.</returns>
    public IReadOnlyList<PluginChangeDto> Unreported()
    {
        var folder = Path.Combine(_installRoot, PluginFolders.Changes);
        if (!Directory.Exists(folder))
        {
            return [];
        }

        return Directory.GetFiles(folder, "*.json")
            .Order(StringComparer.Ordinal)
            .Select(file => Read(Path.GetFileNameWithoutExtension(file)))
            .Where(entry => entry is { Reported: false })
            .Select(entry => entry!.Change)
            .ToList();
    }

    /// <summary>Marks a change as written to the activity log.</summary>
    /// <param name="pluginId">The plugin whose change was reported.</param>
    public void MarkReported(string pluginId)
    {
        var entry = Read(pluginId);
        if (entry is not null)
        {
            Record(entry.Change, reported: true);
        }
    }

    /// <summary>Reads one record file; a missing or unreadable one is no record.</summary>
    /// <param name="pluginId">The plugin id, already checked against the plugin id rule.</param>
    /// <returns>The record; <see langword="null"/> when there is none or it cannot be read.</returns>
    private PluginChangeEntry? Read(string pluginId)
    {
        var file = FileOf(pluginId);
        if (!File.Exists(file))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PluginChangeEntry>(File.ReadAllText(file), JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "The change record of plugin {Id} cannot be read, it is ignored", pluginId);
            return null;
        }
    }

    /// <summary>The path of a plugin's record file.</summary>
    /// <param name="pluginId">The plugin id, already checked against the plugin id rule.</param>
    /// <returns>The file path.</returns>
    private string FileOf(string pluginId)
    {
        return Path.Combine(_installRoot, PluginFolders.Changes, pluginId + ".json");
    }
}
