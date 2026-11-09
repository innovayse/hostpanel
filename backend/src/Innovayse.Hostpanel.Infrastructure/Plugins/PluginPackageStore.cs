using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Hostpanel.Sdk.Packaging.Archive;
using Hostpanel.Sdk.Packaging.Naming;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PluginVersionChange = Hostpanel.Sdk.Packaging.Enums.PluginVersionChange;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// File system implementation of <see cref="IPluginPackageStore"/>. An upload is streamed to a
/// private staging folder, checked in full (size, entry count, manifest, id, paths, assembly names, signature,
/// trusted digests), unpacked there, and only then moved into place with a single folder rename, so
/// a refused or failed install leaves nothing behind and a half-written plugin is never visible. An upload for a
/// plugin that is loaded, or whose version differs from the installed (or already staged) one, is staged once it passed
/// the same checks, not swapped: <see cref="PluginUpgradeApplier"/> swaps it in at the next startup, so the folder of a
/// loaded plugin is never overwritten. An upload that is not provably newer than what the restart installs is refused
/// unless the downgrade is forced.
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

    /// <summary>Checks the detached signature of an unpacked upload and applies the host's policy for unsigned packages.</summary>
    private readonly PluginSignatureVerifier _signatures;

    /// <summary>Tells whether a plugin is loaded, whose folder must then never be overwritten in place.</summary>
    private readonly IPluginRegistry _registry;

    /// <summary>The entry count and uncompressed size limits of an upload, in the form the archive guards of the packaging package take.</summary>
    private PluginArchiveLimits Limits => new(_options.MaxEntryCount, _options.MaxUncompressedBytes);

    /// <summary>Initializes the store.</summary>
    /// <param name="options">Upload folder, limits and the trusted-package list.</param>
    /// <param name="signatures">Checks the detached signature of an unpacked upload and applies the host's policy for unsigned packages.</param>
    /// <param name="registry">Tells whether a plugin is loaded, whose folder must then never be overwritten in place.</param>
    /// <param name="logger">Records why an upload was refused; the response carries only a code.</param>
    public PluginPackageStore(
        IOptions<PluginOptions> options, PluginSignatureVerifier signatures, IPluginRegistry registry, ILogger<PluginPackageStore> logger)
    {
        _options = options.Value;
        _signatures = signatures;
        _registry = registry;
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool HasPackageFolder(string pluginId)
    {
        return (TryResolvePluginFolder(PluginOptions.BuiltInRoot, pluginId, out var builtIn) && Directory.Exists(builtIn))
            || (TryResolvePluginFolder(_options.InstallRoot, pluginId, out var uploaded) && Directory.Exists(uploaded));
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
                return await UnpackAndCommitAsync(archivePath, work, installRoot, sha256, upload, ct);
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
            // An upgrade staged for a removed plugin must not come back at the next startup.
            DiscardStaged(installRoot, id);
            PluginUpgradeApplier.DeleteMarker(installRoot, id);
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

        foreach (var (folder, manifest) in await ReadManifestsAsync(PluginOptions.BuiltInRoot, ct))
        {
            builtInIds.Add(manifest.Id);
            result.Add(new InstalledPluginPackage(manifest, IsBuiltIn: true, folder));
        }

        foreach (var (folder, manifest) in await ReadManifestsAsync(_options.InstallRoot, ct))
        {
            // The loader lets the built-in copy win, so an upload with the same id is never the one listed.
            if (!builtInIds.Contains(manifest.Id))
            {
                // Read only for a valid id: it becomes a path.
                var staged = PluginIdentifier.IsValid(manifest.Id)
                    ? PluginUpgradeApplier.ReadVersion(StagedFolder(_options.InstallRoot, manifest.Id))
                    : null;
                result.Add(new InstalledPluginPackage(manifest, IsBuiltIn: false, folder, staged));
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

    /// <summary>A refusal because the manifest or the plugin assemblies break a manifest rule.</summary>
    /// <param name="pluginId">The plugin id from the manifest.</param>
    /// <param name="sha256">The archive's digest, when it has been computed.</param>
    /// <param name="violation">The rule that is broken.</param>
    /// <returns>The result carrying the rule, so the handler picks its error code.</returns>
    private PluginPackageResult RuleBroken(string pluginId, string? sha256, PluginManifestViolation violation)
    {
        _logger.LogWarning(
            "Plugin install refused: {PluginId} breaks manifest rule {Rule}: {Detail}.", pluginId, violation.Rule, violation.Detail);
        return new PluginPackageResult(PluginPackageOutcome.ManifestRuleBroken, pluginId, sha256, violation.Rule);
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
    /// does not have the plugin id shape (<see cref="PluginIdentifier.IsWellFormed"/>) or that would not
    /// resolve to a direct child of the root. A reserved id is resolved: whether a plugin may take it is
    /// asked by install (<see cref="PluginManifestValidator.Validate"/>), never by removal.
    /// </summary>
    /// <param name="root">The absolute plugin folder.</param>
    /// <param name="id">The candidate id.</param>
    /// <param name="pluginDir">The resolved absolute folder, when the id is accepted.</param>
    /// <returns><see langword="true"/> when the id is accepted.</returns>
    private static bool TryResolvePluginFolder(string root, string? id, out string pluginDir)
    {
        pluginDir = string.Empty;
        if (!PluginIdentifier.IsWellFormed(id))
        {
            return false;
        }

        var resolvedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(resolvedRoot, id));
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
    /// <param name="upload">The upload: whether unsigned packages are allowed and whether a downgrade is forced.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="InvalidDataException">The archive is not a readable ZIP file.</exception>
    private async Task<PluginPackageResult> UnpackAndCommitAsync(
        string archivePath, string work, string installRoot, string sha256, PluginPackageUpload upload, CancellationToken ct)
    {
        using var archive = ZipFile.OpenRead(archivePath);

        // Declared sizes are checked up front so an obviously oversized archive is refused before
        // anything is unpacked; the real byte count is enforced again while unpacking, since the
        // declared size is only what the archive claims.
        var declared = PluginArchiveGuard.CheckDeclared(archive, Limits);
        if (declared is not null)
        {
            _logger.LogWarning("Plugin install refused: the archive exceeds the entry count or size limit ({Rule}).", declared.Rule);
            return Refused(PluginRuleMap.ToHost(declared.Rule));
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

        // Only the shape is checked here, because removal resolves through the same method; a reserved manifest id
        // passes it and is refused below by PluginManifestValidator.Validate, the one place the full id rule is asked.
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

        if (archive.Entries.Any(e => PluginHostAssemblies.IsProvided(e.Name)))
        {
            _logger.LogWarning("Plugin install refused: {PluginId} carries an assembly the host provides.", id);
            return new PluginPackageResult(PluginPackageOutcome.HostAssemblyRefused, id, sha256);
        }

        // LoadFrom binds a name to the first copy loaded, so a DLL name another installed or staged plugin already ships
        // would run this plugin on that plugin's copy. The plugin's own installed and staged folders are skipped (an upgrade).
        var conflictingId = PluginAssemblyConflicts.Find(
            id,
            archive.Entries.Where(e => PluginAssemblyConflicts.IsAssembly(e.Name)).Select(e => e.Name).ToList(),
            installRoot,
            PluginOptions.BuiltInRoot);
        if (conflictingId is not null)
        {
            _logger.LogWarning(
                "Plugin install refused: {PluginId} carries an assembly file name that plugin {OtherPluginId} already ships.", id, conflictingId);
            return new PluginPackageResult(PluginPackageOutcome.AssemblyConflict, id, sha256);
        }

        var violation = PluginManifestValidator.Validate(manifest);
        if (violation is not null)
        {
            return RuleBroken(id, sha256, violation);
        }

        var contentDir = Path.Combine(work, "content");
        var unpacked = Unpack(archive, contentDir, id);
        if (unpacked != PluginPackageOutcome.Installed)
        {
            return new PluginPackageResult(unpacked, id, sha256);
        }

        // The assemblies can only be read once unpacked; the staging folder is thrown away when the check refuses.
        violation = PluginManifestValidator.ValidateSdkReference(contentDir);
        if (violation is not null)
        {
            return RuleBroken(id, sha256, violation);
        }

        // Every view rule applies to an upload, the ones added after plugins were installed included; the loader warns
        // about those instead (PluginViewRead.Advisories), so an installed plugin is not lost to a host upgrade.
        violation = PluginViewDocumentReader.Read(contentDir, manifest, _options.MaxViewBytes).RefusedAtInstall;
        if (violation is not null)
        {
            return RuleBroken(id, sha256, violation);
        }

        violation = PluginIconReader.Read(contentDir, manifest).Violation;
        if (violation is not null)
        {
            return RuleBroken(id, sha256, violation);
        }

        // The signature covers the unpacked files, so it is checked here, after the archive is unpacked and before the move into place.
        var signature = _signatures.Check(contentDir, id, PluginOrigin.Uploaded, upload.AllowUnsigned, _logger, upload.RequireSignature);
        if (signature is not null)
        {
            _logger.LogWarning(
                "Plugin install refused: {PluginId} fails signature rule {Rule}: {Detail}.", id, signature.Rule, signature.Detail);
            return new PluginPackageResult(PluginPackageOutcome.SignatureRefused, id, sha256, SignatureRule: signature.Rule);
        }

        // The folder of a plugin that is loaded is never overwritten in place: its assemblies are in use, so any upload
        // for it, an upgrade, a downgrade or the same version again, waits in the staged folder for the restart. The
        // comparison is against what the restart will install, which is the staged upload when there is one. A version
        // that cannot be compared (the installed manifest is unreadable) is not provably an upgrade and counts as an older one.
        var installedVersion = PluginUpgradeApplier.ReadVersion(targetDir);
        var stagedVersion = PluginUpgradeApplier.ReadVersion(StagedFolder(installRoot, id));
        var reference = stagedVersion ?? installedVersion;
        var isLoaded = _registry.IsLoaded(id);
        if (reference is not null || isLoaded)
        {
            var change = PluginVersion.Compare(reference, manifest.Version);
            if (change is PluginVersionChange.Older or PluginVersionChange.Incomparable && !upload.ForceDowngrade)
            {
                _logger.LogWarning(
                    "Plugin install refused: {PluginId} {Version} is not newer than {Reference} and the downgrade is not forced.",
                    id, manifest.Version, reference);
                return new PluginPackageResult(
                    PluginPackageOutcome.DowngradeRefused, id, sha256, Version: manifest.Version, PreviousVersion: reference);
            }

            if (change != PluginVersionChange.Same || isLoaded || stagedVersion is not null)
            {
                await StageAsync(contentDir, installRoot, id, ct);
                return new PluginPackageResult(
                    PluginPackageOutcome.Staged, id, sha256, Version: manifest.Version, PreviousVersion: reference);
            }
        }

        await CommitAsync(contentDir, targetDir, work, installRoot, id, ct);
        return new PluginPackageResult(PluginPackageOutcome.Installed, id, sha256, Version: manifest.Version);
    }

    /// <summary>
    /// Drops the upgrade staged for a plugin. The folder is first renamed out of the staged area, so a delete that fails
    /// halfway can never leave a half-deleted upgrade for the next startup to swap in; what is left of it is in the work
    /// folder, which the next startup sweeps.
    /// </summary>
    /// <param name="installRoot">The upload folder.</param>
    /// <param name="id">A plugin id that already meets the plugin id rule.</param>
    private void DiscardStaged(string installRoot, string id)
    {
        var staged = StagedFolder(installRoot, id);
        if (!Directory.Exists(staged))
        {
            return;
        }

        var trash = Path.Combine(installRoot, StagingFolderName, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(trash)!);
        Directory.Move(staged, trash);
        DeleteQuietly(trash);
    }

    /// <summary>The folder an upgrade of one plugin waits in until the next startup.</summary>
    /// <param name="installRoot">The upload folder.</param>
    /// <param name="id">A plugin id that already meets the plugin id rule.</param>
    /// <returns>The absolute staged folder path.</returns>
    private static string StagedFolder(string installRoot, string id)
    {
        return Path.Combine(installRoot, PluginFolders.Staged, id);
    }

    /// <summary>
    /// Moves the unpacked upgrade into the staged folder of its plugin, replacing an earlier staged upload of the same
    /// plugin, so the folder always holds the last upload that passed every check.
    /// </summary>
    /// <param name="contentDir">The unpacked plugin in the staging folder.</param>
    /// <param name="installRoot">The upload folder.</param>
    /// <param name="id">The plugin id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the upgrade is staged.</returns>
    private async Task StageAsync(string contentDir, string installRoot, string id, CancellationToken ct)
    {
        await CommitLock.WaitAsync(ct);
        try
        {
            var staged = StagedFolder(installRoot, id);
            DiscardStaged(installRoot, id);

            Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
            Directory.Move(contentDir, staged);
        }
        finally
        {
            CommitLock.Release();
        }
    }

    /// <summary>
    /// Unpacks the archive into <paramref name="contentDir"/> through the packaging package's
    /// <see cref="PluginArchiveGuard.Extract"/>, the one unpack routine, and maps the guard it reports to an outcome.
    /// </summary>
    /// <param name="archive">The open archive.</param>
    /// <param name="contentDir">The staging folder to unpack into.</param>
    /// <param name="id">The plugin id, for the log.</param>
    /// <returns><see cref="PluginPackageOutcome.Installed"/> when everything was unpacked, otherwise the refusal.</returns>
    private PluginPackageOutcome Unpack(ZipArchive archive, string contentDir, string id)
    {
        var violation = PluginArchiveGuard.Extract(archive, contentDir, Limits);
        if (violation is null)
        {
            return PluginPackageOutcome.Installed;
        }

        _logger.LogWarning(
            "Plugin install refused: the archive of {PluginId} breaks archive rule {Rule}: {Detail}.", id, violation.Rule, violation.Detail);
        return PluginRuleMap.ToHost(violation.Rule);
    }

    /// <summary>
    /// Moves the unpacked plugin into place. An earlier upload with the same id is moved aside
    /// first and put back if the final rename fails, so the folder is always either the old plugin
    /// or the new one.
    /// </summary>
    /// <param name="contentDir">The unpacked plugin in the staging folder.</param>
    /// <param name="targetDir">The plugin's final folder.</param>
    /// <param name="work">This install's staging folder, where the earlier upload is set aside.</param>
    /// <param name="installRoot">The upload folder.</param>
    /// <param name="id">The plugin id; an unreadable staged upgrade of it is dropped, because this upload supersedes it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the plugin is in place.</returns>
    private async Task CommitAsync(string contentDir, string targetDir, string work, string installRoot, string id, CancellationToken ct)
    {
        await CommitLock.WaitAsync(ct);
        try
        {
            DiscardStaged(installRoot, id);

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

    /// <summary>
    /// Reads every plugin folder's manifest under a root, skipping folders that are not plugins. A folder whose
    /// name is reserved is still read, so a plugin installed before its id was reserved is listed and can be removed
    /// (the loader refuses to load it).
    /// </summary>
    /// <param name="root">The plugin folder to scan.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The readable manifests, each with the name of the folder it was read from.</returns>
    private async Task<IReadOnlyList<(string Folder, PluginManifest Manifest)>> ReadManifestsAsync(
        string root, CancellationToken ct)
    {
        var manifests = new List<(string Folder, PluginManifest Manifest)>();
        if (!Directory.Exists(root))
        {
            return manifests;
        }

        foreach (var dir in Directory.GetDirectories(root))
        {
            var manifestPath = Path.Combine(dir, ManifestFileName);
            if (!PluginIdentifier.IsWellFormed(Path.GetFileName(dir)) || !File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                var manifest = PluginManifestReader.Deserialize(await File.ReadAllTextAsync(manifestPath, ct));
                if (manifest is not null)
                {
                    manifests.Add((Path.GetFileName(dir), manifest));
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
