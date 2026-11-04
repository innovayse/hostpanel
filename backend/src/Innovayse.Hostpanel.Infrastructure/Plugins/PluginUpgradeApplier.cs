using System.Text.Json;
using Innovayse.Hostpanel.Domain.Plugins;
using Innovayse.Hostpanel.Domain.Plugins.Enums;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Swaps staged plugin upgrades into place and keeps the record of upgrades whose migration hook is still due. The
/// swap runs once at startup, before the loader scans the plugin folders, so no assembly of the old version is loaded
/// when its folder is replaced.
/// </summary>
/// <remarks>
/// The record is the intent and is written <b>before</b> the swap, atomically, so no crash can leave a swapped plugin
/// without it. It is deleted only after the migration hook succeeded and was audited.
/// </remarks>
internal static class PluginUpgradeApplier
{
    /// <summary>The name prefix of the folder the replaced version is set aside in while the swap runs.</summary>
    private const string PreviousPrefix = "previous-";

    /// <summary>The extension a record has while it is being written; renamed over the real file when complete.</summary>
    private const string TempExtension = ".tmp";

    /// <summary>JSON options of the upgrade record files.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// Recovers an interrupted swap, sweeps the work folders an earlier run left behind, then replaces each installed
    /// plugin that has a staged upgrade with the staged files. A staged folder that cannot be read, or that the swap
    /// fails on, is logged and left out: the installed plugin keeps running and the next restart tries again.
    /// </summary>
    /// <param name="installRoot">The upload folder.</param>
    /// <param name="logger">Records each swap and each staged upgrade that is skipped.</param>
    public static void Apply(string installRoot, ILogger logger)
    {
        RecoverInterruptedSwaps(installRoot, logger);
        SweepScratch(installRoot, logger);

        var stagedRoot = Path.Combine(installRoot, PluginFolders.Staged);
        if (!Directory.Exists(stagedRoot))
        {
            return;
        }

        foreach (var staged in Directory.GetDirectories(stagedRoot))
        {
            var id = Path.GetFileName(staged);
            var toVersion = ReadVersion(staged);
            if (!PluginIdentifier.IsValid(id) || toVersion is null)
            {
                logger.LogWarning("Staged upgrade in {Dir} is not a readable plugin, skipping it", staged);
                continue;
            }

            SwapOne(installRoot, id, staged, toVersion, logger);
        }
    }

    /// <summary>
    /// Reads the upgrades that were swapped in (or were about to be) and whose migration hook is still due. A record
    /// that cannot be read is logged with its file name and rebuilt from the file name alone, so the plugin it names stays
    /// gated and its hook still runs.
    /// </summary>
    /// <param name="installRoot">The upload folder.</param>
    /// <param name="logger">Records each record that cannot be read.</param>
    /// <returns>The pending records, in plugin id order; empty when none.</returns>
    public static IReadOnlyList<PluginUpgradeMarker> ReadPending(string installRoot, ILogger logger)
    {
        var folder = Path.Combine(installRoot, PluginFolders.Upgrades);
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var markers = new List<PluginUpgradeMarker>();
        foreach (var file in Directory.GetFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            var marker = ReadFile(installRoot, file, logger);
            if (marker is not null)
            {
                markers.Add(marker);
            }
        }

        return markers;
    }

    /// <summary>Deletes a plugin's pending record, if any: once its migration ran and was audited, or when the plugin is removed.</summary>
    /// <param name="installRoot">The upload folder.</param>
    /// <param name="pluginId">The plugin whose record is deleted, already checked against the plugin id rule.</param>
    public static void DeleteMarker(string installRoot, string pluginId)
    {
        var file = MarkerPath(installRoot, pluginId);
        if (File.Exists(file))
        {
            File.Delete(file);
        }
    }

    /// <summary>Reads the version a plugin folder's manifest declares.</summary>
    /// <param name="pluginDir">A plugin folder, installed or staged.</param>
    /// <returns>The version text; <see langword="null"/> when the folder or its manifest is missing or unreadable.</returns>
    public static string? ReadVersion(string pluginDir)
    {
        var manifestPath = Path.Combine(pluginDir, PluginFolders.ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var version = PluginManifestReader.Deserialize(File.ReadAllText(manifestPath))?.Version;
            return string.IsNullOrWhiteSpace(version) ? null : version;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Swaps one staged upgrade in. The record is written first; when the swap then fails the record is put back as it
    /// was, because nothing changed.
    /// </summary>
    /// <param name="installRoot">The upload folder.</param>
    /// <param name="id">The plugin id, already checked against the plugin id rule.</param>
    /// <param name="staged">The staged folder.</param>
    /// <param name="toVersion">The staged version.</param>
    /// <param name="logger">Records the swap or why it was not done.</param>
    private static void SwapOne(string installRoot, string id, string staged, string toVersion, ILogger logger)
    {
        var target = Path.Combine(installRoot, id);
        var fromVersion = ReadVersion(target);

        // A record that is still pending belongs to an upgrade whose hook never succeeded; the first FromVersion is the
        // data's real origin, so a second upgrade on top keeps it instead of overwriting it with the intermediate version.
        var earlier = ReadPending(installRoot, logger).FirstOrDefault(m => m.PluginId == id);
        var needsRecord = earlier is not null
            || fromVersion is null
            || PluginVersion.Compare(fromVersion, toVersion) != PluginVersionChange.Same;
        var marker = new PluginUpgradeMarker(id, earlier?.FromVersion ?? fromVersion, toVersion);

        var previous = Path.Combine(installRoot, PluginFolders.Scratch, PreviousPrefix + id);
        var movedAside = false;
        try
        {
            if (needsRecord)
            {
                WriteMarker(installRoot, marker);
            }

            if (Directory.Exists(target))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(previous)!);
                Directory.Move(target, previous);
                movedAside = true;
            }

            Directory.Move(staged, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            RestoreAfterFailedSwap(installRoot, id, target, previous, movedAside, needsRecord, earlier, logger);
            logger.LogError(
                ex, "Could not swap in the staged upgrade of plugin {Id}, the installed version stays and the next start tries again", id);
            return;
        }

        logger.LogInformation("Plugin {Id} upgraded from {From} to {To}", id, marker.FromVersion, toVersion);
        if (movedAside)
        {
            DeleteQuietly(previous, logger);
        }
    }

    /// <summary>Puts the installed version and the record back after a swap that failed halfway.</summary>
    /// <param name="installRoot">The upload folder.</param>
    /// <param name="id">The plugin id.</param>
    /// <param name="target">The plugin's installed folder.</param>
    /// <param name="previous">The folder the installed version was set aside in.</param>
    /// <param name="movedAside">Whether the installed version was moved to <paramref name="previous"/>.</param>
    /// <param name="wroteRecord">Whether the swap wrote a record before it failed.</param>
    /// <param name="earlier">The record that was pending before the swap; <see langword="null"/> when there was none.</param>
    /// <param name="logger">Records a restore that itself failed.</param>
    private static void RestoreAfterFailedSwap(
        string installRoot,
        string id,
        string target,
        string previous,
        bool movedAside,
        bool wroteRecord,
        PluginUpgradeMarker? earlier,
        ILogger logger)
    {
        try
        {
            if (movedAside && !Directory.Exists(target))
            {
                Directory.Move(previous, target);
            }

            if (wroteRecord)
            {
                if (earlier is null)
                {
                    DeleteMarker(installRoot, id);
                }
                else
                {
                    WriteMarker(installRoot, earlier);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The previous folder is kept; the next start's recovery moves it back.
            logger.LogError(ex, "Could not restore plugin {Id} after the failed swap; the next start recovers it", id);
        }
    }

    /// <summary>
    /// Finishes what a crash interrupted: a set-aside version whose installed folder is missing is moved back (the
    /// swap is retried from the staged folder), and one whose installed folder exists is deleted.
    /// </summary>
    /// <param name="installRoot">The upload folder.</param>
    /// <param name="logger">Records each recovery.</param>
    private static void RecoverInterruptedSwaps(string installRoot, ILogger logger)
    {
        var scratch = Path.Combine(installRoot, PluginFolders.Scratch);
        if (!Directory.Exists(scratch))
        {
            return;
        }

        foreach (var folder in Directory.GetDirectories(scratch))
        {
            var name = Path.GetFileName(folder);
            if (!name.StartsWith(PreviousPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var id = name[PreviousPrefix.Length..];
            var target = Path.Combine(installRoot, id);
            if (!PluginIdentifier.IsValid(id) || Directory.Exists(target))
            {
                continue;
            }

            try
            {
                Directory.Move(folder, target);
                logger.LogWarning("Plugin {Id} was left set aside by an interrupted swap, restored it", id);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogError(ex, "Could not restore plugin {Id} from {Dir}", id, folder);
            }
        }
    }

    /// <summary>
    /// Deletes what an install, a removal or a swap left in the work folder when the host stopped, and any record that was
    /// being written. Safe at startup: nothing runs yet, and a set-aside version that matters was recovered first.
    /// </summary>
    /// <param name="installRoot">The upload folder.</param>
    /// <param name="logger">Records what could not be deleted.</param>
    private static void SweepScratch(string installRoot, ILogger logger)
    {
        var scratch = Path.Combine(installRoot, PluginFolders.Scratch);
        if (Directory.Exists(scratch))
        {
            foreach (var folder in Directory.GetDirectories(scratch))
            {
                // A set-aside version that could not be moved back is the only copy of an installed plugin: keep it.
                var name = Path.GetFileName(folder);
                if (name.StartsWith(PreviousPrefix, StringComparison.Ordinal)
                    && !Directory.Exists(Path.Combine(installRoot, name[PreviousPrefix.Length..])))
                {
                    logger.LogError("Keeping {Dir}: it holds the only copy of a plugin whose swap was interrupted", folder);
                    continue;
                }

                DeleteQuietly(folder, logger);
            }
        }

        var upgrades = Path.Combine(installRoot, PluginFolders.Upgrades);
        if (Directory.Exists(upgrades))
        {
            foreach (var file in Directory.GetFiles(upgrades, "*" + TempExtension))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(ex, "Could not delete the unfinished upgrade record {File}", Path.GetFileName(file));
                }
            }
        }
    }

    /// <summary>Deletes a folder, logging instead of throwing when the file system refuses.</summary>
    /// <param name="folder">The folder to delete.</param>
    /// <param name="logger">Records a failure.</param>
    private static void DeleteQuietly(string folder, ILogger logger)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not delete the leftover folder {Dir}", folder);
        }
    }

    /// <summary>Reads one record file; an unreadable one is logged and rebuilt from its file name.</summary>
    /// <param name="installRoot">The upload folder.</param>
    /// <param name="file">The record file.</param>
    /// <param name="logger">Records an unreadable file with its name.</param>
    /// <returns>The record; <see langword="null"/> when the file name is not a plugin id either.</returns>
    private static PluginUpgradeMarker? ReadFile(string installRoot, string file, ILogger logger)
    {
        var name = Path.GetFileName(file);
        var id = Path.GetFileNameWithoutExtension(file);
        try
        {
            var marker = JsonSerializer.Deserialize<PluginUpgradeMarker>(File.ReadAllText(file), JsonOptions);
            if (marker is not null
                && string.Equals(marker.PluginId, id, StringComparison.Ordinal)
                && PluginIdentifier.IsValid(marker.PluginId)
                && !string.IsNullOrWhiteSpace(marker.ToVersion))
            {
                return marker;
            }

            logger.LogError("Upgrade record {File} is incomplete or names another plugin", name);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Upgrade record {File} cannot be read", name);
        }

        if (!PluginIdentifier.IsValid(id))
        {
            return null;
        }

        // The file name still says which plugin has an upgrade whose migration is due. Failing closed keeps that plugin
        // gated and runs its (idempotent) hook, with the installed version as the best known target.
        return new PluginUpgradeMarker(id, null, ReadVersion(Path.Combine(installRoot, id)) ?? "unknown");
    }

    /// <summary>Writes a pending record atomically: to a temporary file in the same folder, then renamed over the record.</summary>
    /// <param name="installRoot">The upload folder.</param>
    /// <param name="marker">The record.</param>
    private static void WriteMarker(string installRoot, PluginUpgradeMarker marker)
    {
        Directory.CreateDirectory(Path.Combine(installRoot, PluginFolders.Upgrades));
        var file = MarkerPath(installRoot, marker.PluginId);
        var temp = file + TempExtension;
        File.WriteAllText(temp, JsonSerializer.Serialize(marker, JsonOptions));
        File.Move(temp, file, overwrite: true);
    }

    /// <summary>The path of a plugin's pending record.</summary>
    /// <param name="installRoot">The upload folder.</param>
    /// <param name="pluginId">The plugin id, already checked against the plugin id rule.</param>
    /// <returns>The file path.</returns>
    private static string MarkerPath(string installRoot, string pluginId)
    {
        return Path.Combine(installRoot, PluginFolders.Upgrades, pluginId + ".json");
    }
}
