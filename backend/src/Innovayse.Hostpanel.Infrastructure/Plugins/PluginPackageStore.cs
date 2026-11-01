using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Plugins;
using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Innovayse.SDK.Plugins.Manifest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// File system implementation of <see cref="IPluginPackageStore"/>. An upload is streamed to a
/// private staging folder, checked in full (size, entry count, manifest, id, paths, assembly names,
/// trusted digests), unpacked there, and only then moved into place with a single folder rename, so
/// a refused or failed install leaves nothing behind and a half-written plugin is never visible.
/// </summary>
public sealed class PluginPackageStore : IPluginPackageStore
{
    /// <summary>The manifest's file name at the root of every plugin archive and folder.</summary>
    private const string ManifestFileName = "plugin.json";

    /// <summary>
    /// The staging folder's name inside the upload folder. It fails the plugin id rule, so the
    /// loader and the list never take it for a plugin.
    /// </summary>
    private const string StagingFolderName = ".staging";

    /// <summary>The buffer size used to stream the upload and the unpacked entries.</summary>
    private const int BufferSize = 81_920;

    /// <summary>
    /// Serialises the final rename and removals, so two admins acting on the same id at once cannot
    /// interleave a replace with a delete.
    /// </summary>
    private static readonly SemaphoreSlim CommitLock = new(1, 1);

    /// <summary>Upload folder, limits and the trusted-package list.</summary>
    private readonly PluginOptions _options;

    /// <summary>Records why an upload was refused; the response carries only a code.</summary>
    private readonly ILogger<PluginPackageStore> _logger;

    /// <summary>Initializes the store.</summary>
    /// <param name="options">Upload folder, limits and the trusted-package list.</param>
    /// <param name="logger">Records why an upload was refused; the response carries only a code.</param>
    public PluginPackageStore(IOptions<PluginOptions> options, ILogger<PluginPackageStore> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PluginPackageResult> InstallAsync(PluginPackageUpload upload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(upload);

        var installRoot = _options.InstallRoot;
        var work = Path.Combine(installRoot, StagingFolderName, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);

        try
        {
            var archivePath = Path.Combine(work, "upload.zip");
            var sha256 = await SaveUploadAsync(upload.Content, archivePath, ct);
            if (new FileInfo(archivePath).Length == 0)
            {
                _logger.LogWarning("Plugin install refused: the upload is empty.");
                return Refused(PluginPackageOutcome.ManifestInvalid);
            }

            if (_options.TrustedPackageSha256.Count > 0
                && !_options.TrustedPackageSha256.Contains(sha256, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Plugin install refused: archive {Sha256} is not on the trusted list.", sha256);
                return new PluginPackageResult(PluginPackageOutcome.NotTrusted, null, sha256);
            }

            try
            {
                return await UnpackAndCommitAsync(archivePath, work, installRoot, sha256, ct);
            }
            catch (InvalidDataException ex)
            {
                _logger.LogWarning(ex, "Plugin install refused: the upload is not a readable ZIP archive.");
                return Refused(PluginPackageOutcome.ManifestInvalid);
            }
        }
        finally
        {
            DeleteQuietly(work);
        }
    }

    /// <inheritdoc/>
    public async Task<PluginPackageResult> RemoveAsync(PluginPackageRemoval removal, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(removal);

        var id = removal.PluginId;
        var installRoot = _options.InstallRoot;
        if (!TryResolvePluginFolder(installRoot, id, out var pluginDir))
        {
            return Refused(PluginPackageOutcome.IdInvalid);
        }

        if (IsBuiltIn(id))
        {
            return new PluginPackageResult(PluginPackageOutcome.BuiltIn, id, null);
        }

        await CommitLock.WaitAsync(ct);
        try
        {
            if (!Directory.Exists(pluginDir))
            {
                return new PluginPackageResult(PluginPackageOutcome.NotFound, id, null);
            }

            // Renamed out of the scanned folder first, so a delete that fails halfway leaves no
            // partial plugin for the next startup to load.
            var trash = Path.Combine(installRoot, StagingFolderName, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.GetDirectoryName(trash)!);
            Directory.Move(pluginDir, trash);
            DeleteQuietly(trash);
        }
        finally
        {
            CommitLock.Release();
        }

        return new PluginPackageResult(PluginPackageOutcome.Removed, id, null);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<InstalledPluginPackage>> ListAsync(CancellationToken ct)
    {
        var result = new List<InstalledPluginPackage>();
        var builtInIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var manifest in await ReadManifestsAsync(PluginOptions.BuiltInRoot, ct))
        {
            builtInIds.Add(manifest.Id);
            result.Add(new InstalledPluginPackage(manifest, IsBuiltIn: true));
        }

        foreach (var manifest in await ReadManifestsAsync(_options.InstallRoot, ct))
        {
            // The loader lets the built-in copy win, so an upload with the same id is never the one listed.
            if (!builtInIds.Contains(manifest.Id))
            {
                result.Add(new InstalledPluginPackage(manifest, IsBuiltIn: false));
            }
        }

        return result;
    }

    /// <summary>A refusal that concerns no particular plugin id.</summary>
    /// <param name="outcome">The refusal.</param>
    /// <returns>The result.</returns>
    private static PluginPackageResult Refused(PluginPackageOutcome outcome)
    {
        return new PluginPackageResult(outcome, null, null);
    }

    /// <summary>Whether a plugin with this id ships with the build.</summary>
    /// <param name="id">A plugin id that already meets the plugin id rule.</param>
    /// <returns><see langword="true"/> when the build output's plugin folder holds it.</returns>
    private static bool IsBuiltIn(string id)
    {
        return Directory.Exists(Path.Combine(PluginOptions.BuiltInRoot, id));
    }

    /// <summary>
    /// Resolves the folder a plugin id names under <paramref name="root"/>, refusing any id that
    /// breaks the plugin id rule or that would not resolve to a direct child of the root.
    /// </summary>
    /// <param name="root">The absolute plugin folder.</param>
    /// <param name="id">The candidate id.</param>
    /// <param name="pluginDir">The resolved absolute folder, when the id is accepted.</param>
    /// <returns><see langword="true"/> when the id is accepted.</returns>
    private static bool TryResolvePluginFolder(string root, string? id, out string pluginDir)
    {
        pluginDir = string.Empty;
        if (!PluginIdentifier.IsValid(id))
        {
            return false;
        }

        var resolvedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(resolvedRoot, id!));
        if (!string.Equals(Path.GetDirectoryName(candidate), resolvedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        pluginDir = candidate;
        return true;
    }

    /// <summary>Streams the upload to disk while hashing it, without holding it in memory.</summary>
    /// <param name="content">The uploaded stream.</param>
    /// <param name="archivePath">Where to write it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The lower-case hex SHA-256 of what was written.</returns>
    private static async Task<string> SaveUploadAsync(
        Stream content, string archivePath, CancellationToken ct)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[BufferSize];

        await using (var file = new FileStream(
            archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
        {
            int read;
            while ((read = await content.ReadAsync(buffer, ct)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>Removes a folder, logging rather than throwing when the file system refuses.</summary>
    /// <param name="path">The folder to remove.</param>
    private void DeleteQuietly(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not remove the plugin staging folder {Path}.", path);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Could not remove the plugin staging folder {Path}.", path);
        }
    }

    /// <summary>
    /// Checks the saved archive, unpacks it into the staging folder and moves it into place.
    /// </summary>
    /// <param name="archivePath">The saved upload.</param>
    /// <param name="work">This install's private staging folder.</param>
    /// <param name="installRoot">The upload folder.</param>
    /// <param name="sha256">The archive's digest, carried into the result for the audit line.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="InvalidDataException">The archive is not a readable ZIP file.</exception>
    private async Task<PluginPackageResult> UnpackAndCommitAsync(
        string archivePath, string work, string installRoot, string sha256, CancellationToken ct)
    {
        using var archive = ZipFile.OpenRead(archivePath);

        // Declared sizes are checked up front so an obviously oversized archive is refused before
        // anything is unpacked; the real byte count is enforced again while unpacking, since the
        // declared size is only what the archive claims.
        if (archive.Entries.Count > _options.MaxEntryCount
            || archive.Entries.Sum(e => e.Length) > _options.MaxUncompressedBytes)
        {
            _logger.LogWarning("Plugin install refused: the archive exceeds the entry count or size limit.");
            return Refused(PluginPackageOutcome.ArchiveTooLarge);
        }

        var manifestEntry = archive.GetEntry(ManifestFileName);
        if (manifestEntry is null)
        {
            _logger.LogWarning("Plugin install refused: plugin.json not found in the archive root.");
            return Refused(PluginPackageOutcome.ManifestInvalid);
        }

        string json;
        using (var reader = new StreamReader(manifestEntry.Open()))
        {
            json = await reader.ReadToEndAsync(ct);
        }

        PluginManifest? manifest;
        try
        {
            manifest = PluginManifestReader.Deserialize(json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Plugin install refused: plugin.json could not be parsed.");
            return Refused(PluginPackageOutcome.ManifestInvalid);
        }

        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Id))
        {
            _logger.LogWarning("Plugin install refused: the manifest has no id.");
            return Refused(PluginPackageOutcome.ManifestInvalid);
        }

        if (manifest.SdkVersion != PluginManifestReader.SupportedSdkVersion)
        {
            _logger.LogWarning(
                "Plugin install refused: the plugin requires SDK version '{Requested}', supported is '{Supported}'.",
                manifest.SdkVersion, PluginManifestReader.SupportedSdkVersion);
            return Refused(PluginPackageOutcome.SdkVersionUnsupported);
        }

        if (!TryResolvePluginFolder(installRoot, manifest.Id, out var targetDir))
        {
            _logger.LogWarning("Plugin install refused: the manifest id does not meet the plugin id rule.");
            return Refused(PluginPackageOutcome.IdInvalid);
        }

        var id = manifest.Id;
        if (IsBuiltIn(id))
        {
            _logger.LogWarning("Plugin install refused: {PluginId} ships with the build.", id);
            return new PluginPackageResult(PluginPackageOutcome.BuiltIn, id, sha256);
        }

        if (archive.Entries.Any(e => HostAssemblyNames.IsHostAssembly(e.Name)))
        {
            _logger.LogWarning("Plugin install refused: {PluginId} carries an assembly the host provides.", id);
            return new PluginPackageResult(PluginPackageOutcome.HostAssemblyRefused, id, sha256);
        }

        var contentDir = Path.Combine(work, "content");
        var unpacked = await UnpackAsync(archive, contentDir, id, ct);
        if (unpacked != PluginPackageOutcome.Installed)
        {
            return new PluginPackageResult(unpacked, id, sha256);
        }

        await CommitAsync(contentDir, targetDir, work, ct);
        return new PluginPackageResult(PluginPackageOutcome.Installed, id, sha256);
    }

    /// <summary>Unpacks every entry into <paramref name="contentDir"/>, counting the real bytes written.</summary>
    /// <param name="archive">The open archive.</param>
    /// <param name="contentDir">The staging folder to unpack into.</param>
    /// <param name="id">The plugin id, for the log.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="PluginPackageOutcome.Installed"/> when everything was unpacked, otherwise the refusal.</returns>
    private async Task<PluginPackageOutcome> UnpackAsync(
        ZipArchive archive, string contentDir, string id, CancellationToken ct)
    {
        Directory.CreateDirectory(contentDir);
        var resolvedContentDir = Path.GetFullPath(contentDir) + Path.DirectorySeparatorChar;
        var buffer = new byte[BufferSize];
        long written = 0;

        foreach (var entry in archive.Entries)
        {
            var targetPath = Path.GetFullPath(Path.Combine(contentDir, entry.FullName.Replace('\\', '/')));

            // Every entry must land inside this plugin's own folder.
            if (!targetPath.StartsWith(resolvedContentDir, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Plugin install refused: an entry of {PluginId} would write outside the plugin folder.", id);
                return PluginPackageOutcome.ArchiveUnsafe;
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                // A folder entry: nothing to write.
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            await using var source = entry.Open();
            await using var target = new FileStream(
                targetPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                written += read;
                if (written > _options.MaxUncompressedBytes)
                {
                    _logger.LogWarning("Plugin install refused: {PluginId} unpacks past the size limit.", id);
                    return PluginPackageOutcome.ArchiveTooLarge;
                }

                await target.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }

        return PluginPackageOutcome.Installed;
    }

    /// <summary>
    /// Moves the unpacked plugin into place. An earlier upload with the same id is moved aside
    /// first and put back if the final rename fails, so the folder is always either the old plugin
    /// or the new one.
    /// </summary>
    /// <param name="contentDir">The unpacked plugin in the staging folder.</param>
    /// <param name="targetDir">The plugin's final folder.</param>
    /// <param name="work">This install's staging folder, where the earlier upload is set aside.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the plugin is in place.</returns>
    private static async Task CommitAsync(string contentDir, string targetDir, string work, CancellationToken ct)
    {
        await CommitLock.WaitAsync(ct);
        try
        {
            var previous = Path.Combine(work, "previous");
            var replaced = Directory.Exists(targetDir);
            if (replaced)
            {
                Directory.Move(targetDir, previous);
            }

            try
            {
                Directory.Move(contentDir, targetDir);
            }
            catch (IOException) when (replaced)
            {
                Directory.Move(previous, targetDir);
                throw;
            }
        }
        finally
        {
            CommitLock.Release();
        }
    }

    /// <summary>Reads every plugin folder's manifest under a root, skipping folders that are not plugins.</summary>
    /// <param name="root">The plugin folder to scan.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The readable manifests.</returns>
    private async Task<IReadOnlyList<PluginManifest>> ReadManifestsAsync(
        string root, CancellationToken ct)
    {
        var manifests = new List<PluginManifest>();
        if (!Directory.Exists(root))
        {
            return manifests;
        }

        foreach (var dir in Directory.GetDirectories(root))
        {
            var manifestPath = Path.Combine(dir, ManifestFileName);
            if (!PluginIdentifier.IsValid(Path.GetFileName(dir)) || !File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                var manifest = PluginManifestReader.Deserialize(await File.ReadAllTextAsync(manifestPath, ct));
                if (manifest is not null)
                {
                    manifests.Add(manifest);
                }
            }
            catch (JsonException ex)
            {
                // Logged and skipped: one broken manifest must not hide the other plugins.
                _logger.LogWarning(ex, "Skipping the plugin in {Dir}: its plugin.json could not be parsed.", dir);
            }
        }

        return manifests;
    }
}
