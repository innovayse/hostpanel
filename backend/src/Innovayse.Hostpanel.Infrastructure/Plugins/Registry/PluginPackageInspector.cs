using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Hostpanel.Sdk.Packaging.Signing;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Plugins.Registry;

/// <summary>
/// Reads the plugin id, the version and the signing key id an archive declares, from the two root files, and answers
/// whether a key id is one the operator added. The files are parsed by the code the package store and the loader use
/// (<see cref="PluginManifestReader"/> and <see cref="PluginSignatureFile"/>), never by a parser of its own, so the
/// values here are the ones the store sees. A file is read from a bounded stream, whatever length the archive claims.
/// The package store still verifies the signature, so a lie here is caught there.
/// </summary>
public sealed class PluginPackageInspector : IPluginPackageInspector
{
    /// <summary>The manifest's file name at the archive root.</summary>
    internal const string ManifestEntry = "plugin.json";

    /// <summary>The most bytes read from the manifest.</summary>
    internal const int MaxManifestBytes = 262_144;

    /// <summary>The trusted keys; only the operator's list is asked.</summary>
    private readonly PluginSigningOptions _signing;

    /// <summary>Initializes the inspector.</summary>
    /// <param name="signing">The trusted keys.</param>
    public PluginPackageInspector(IOptions<PluginSigningOptions> signing)
    {
        _signing = signing.Value;
    }

    /// <inheritdoc/>
    public Task<PluginPackageHeader> ReadHeaderAsync(Stream package, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            using var archive = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);

            // Two entries that differ only in case are the same file once unpacked on a case-insensitive system and two
            // files on another, so which one the store sees is not defined: such an archive says nothing.
            if (HasAmbiguousEntry(archive, ManifestEntry) || HasAmbiguousEntry(archive, PluginSignatureFile.FileName))
            {
                return Task.FromResult(Unknown);
            }

            var manifest = ReadManifest(ReadText(archive, ManifestEntry, MaxManifestBytes));
            var key = ReadKeyId(ReadText(archive, PluginSignatureFile.FileName, PluginSignatureFile.MaxFileBytes));
            return Task.FromResult(new PluginPackageHeader(manifest?.Id, manifest?.Version, key));
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException or ArgumentException)
        {
            return Task.FromResult(Unknown);
        }
    }

    /// <inheritdoc/>
    public Task<PluginManifest?> ReadManifestAsync(Stream package, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            using var archive = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);

            // The same refusal as the header: which of two case variants the store would see is not defined.
            if (HasAmbiguousEntry(archive, ManifestEntry))
            {
                return Task.FromResult<PluginManifest?>(null);
            }

            return Task.FromResult(ReadManifest(ReadText(archive, ManifestEntry, MaxManifestBytes)));
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException or ArgumentException)
        {
            return Task.FromResult<PluginManifest?>(null);
        }
    }

    /// <inheritdoc/>
    public bool IsOperatorKey(string keyId)
    {
        return _signing.OperatorKeys.Any(key => string.Equals(key.KeyId, keyId, StringComparison.Ordinal));
    }

    /// <summary>The header of an archive that says nothing usable.</summary>
    private static PluginPackageHeader Unknown => new(null, null, null);

    /// <summary>Whether more than one entry has a name equal to <paramref name="name"/> ignoring case.</summary>
    /// <param name="archive">The archive.</param>
    /// <param name="name">The file name at the root.</param>
    /// <returns><see langword="true"/> when two or more entries match.</returns>
    private static bool HasAmbiguousEntry(ZipArchive archive, string name)
    {
        return archive.Entries.Count(e => string.Equals(e.FullName, name, StringComparison.OrdinalIgnoreCase)) > 1;
    }

    /// <summary>Reads a root file as UTF-8 text through a stream bounded at the cap plus one byte.</summary>
    /// <param name="archive">The archive.</param>
    /// <param name="name">The file name at the root (exact case).</param>
    /// <param name="maxBytes">The cap; a longer file reads as missing.</param>
    /// <returns>The text; <see langword="null"/> for a missing or oversized file.</returns>
    private static string? ReadText(ZipArchive archive, string name, int maxBytes)
    {
        var entry = archive.GetEntry(name);
        if (entry is null || !string.Equals(entry.FullName, name, StringComparison.Ordinal))
        {
            return null;
        }

        // The length the archive declares is not trusted: the read itself stops one byte past the cap.
        using var stream = entry.Open();
        using var content = new MemoryStream();
        var chunk = new byte[4096];
        while (true)
        {
            var wanted = (int)Math.Min(chunk.Length, (long)maxBytes + 1 - content.Length);
            var read = stream.Read(chunk, 0, wanted);
            if (read == 0)
            {
                return new UTF8Encoding(false, true).GetString(content.ToArray());
            }

            content.Write(chunk, 0, read);
            if (content.Length > maxBytes)
            {
                return null;
            }
        }
    }

    /// <summary>Parses the manifest text the way the loader and the store do.</summary>
    /// <param name="json">The text; may be <see langword="null"/>.</param>
    /// <returns>The manifest; <see langword="null"/> when there is none or it cannot be parsed.</returns>
    private static PluginManifest? ReadManifest(string? json)
    {
        if (json is null)
        {
            return null;
        }

        try
        {
            return PluginManifestReader.Deserialize(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Parses the signature text with the shared signature-file parser.</summary>
    /// <param name="json">The text; may be <see langword="null"/>.</param>
    /// <returns>The key id; <see langword="null"/> when there is no file or it is not a signature file.</returns>
    private static string? ReadKeyId(string? json)
    {
        if (json is null)
        {
            return null;
        }

        try
        {
            return PluginSignatureFile.Parse(json).KeyId;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
