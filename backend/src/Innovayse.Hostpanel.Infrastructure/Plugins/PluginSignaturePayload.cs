using System.Security.Cryptography;
using System.Text;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The exact bytes a plugin package signature covers, built the same way when a package is signed and when it is
/// verified, so the two cannot drift apart.
/// </summary>
/// <remarks>
/// <para>
/// The text is UTF-8 without a byte order mark, every line ends in a line feed, and it reads:
/// </para>
/// <code>
/// innovayse-plugin-signature-v1
/// key &lt;keyId&gt;
/// &lt;sha256&gt; &lt;path&gt;      (one line per file)
/// </code>
/// <para>
/// A file's line is its lower-case hex SHA-256 (64 characters), one space, then its path relative to the package
/// root with forward slashes. The lines are sorted by path with ordinal comparison, so the order does not depend on
/// the file system or the culture. Every file under the package root is listed, in sub-folders too, except
/// <c>plugin.sig</c> at the root, which is the signature itself. Folders are not listed, so an empty folder is not
/// part of what is signed. The key id is part of the payload, so a signature cannot be presented under another key id.
/// </para>
/// <para>
/// A path holding a control character (a line feed in a file name would let one file stand for two lines), a path
/// that is not valid Unicode (a lone surrogate has no UTF-8 form), and a file or folder that is a reparse point (a
/// symbolic link or a junction, which could point outside the package or change after signing) cannot be signed
/// unambiguously and make <see cref="Build"/> throw. Links are never followed.
/// </para>
/// </remarks>
public static class PluginSignaturePayload
{
    /// <summary>The first line: names the format, so a later change to it gets a new name instead of a silent clash.</summary>
    public const string Header = "innovayse-plugin-signature-v1";

    /// <summary>The prefix of the key id line.</summary>
    private const string KeyLinePrefix = "key ";

    /// <summary>UTF-8 without a byte order mark that throws on a character it cannot encode instead of writing a replacement.</summary>
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Lists everything, hidden and system entries too, and does not recurse: the walk below does that itself.</summary>
    private static readonly EnumerationOptions ListEverything = new()
    {
        AttributesToSkip = 0,
        RecurseSubdirectories = false,
        IgnoreInaccessible = false,
    };

    /// <summary>The buffer size used to hash a file.</summary>
    private const int BufferSize = 81_920;

    /// <summary>Builds the payload of the package in a folder.</summary>
    /// <param name="packageDir">The package root: the folder holding <c>plugin.json</c>.</param>
    /// <param name="keyId">The id of the key that signs, or is claimed to have signed.</param>
    /// <returns>The bytes that are signed.</returns>
    /// <exception cref="InvalidDataException">The key id is not valid, or a file or folder in the package cannot be signed unambiguously: its name holds a control character or is not valid Unicode, or it is a link.</exception>
    public static byte[] Build(string packageDir, string keyId)
    {
        ArgumentException.ThrowIfNullOrEmpty(packageDir);
        if (!TrustedKeyId.IsValid(keyId))
        {
            throw new InvalidDataException("The key id is not a valid key id.");
        }

        var root = Path.GetFullPath(packageDir);
        var lines = new List<(string Path, string Hash)>();
        Collect(new DirectoryInfo(root), root, lines);

        lines.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));

        var text = new StringBuilder()
            .Append(Header).Append('\n')
            .Append(KeyLinePrefix).Append(keyId).Append('\n');
        foreach (var (path, hash) in lines)
        {
            text.Append(hash).Append(' ').Append(path).Append('\n');
        }

        return Encode(text.ToString());
    }

    /// <summary>Encodes the payload text strictly: text that has no UTF-8 form is refused rather than written with a replacement character.</summary>
    /// <param name="text">The payload text.</param>
    /// <returns>The UTF-8 bytes, without a byte order mark.</returns>
    /// <exception cref="InvalidDataException">The text holds a character that cannot be encoded, such as a lone surrogate in a file name.</exception>
    internal static byte[] Encode(string text)
    {
        try
        {
            return StrictUtf8.GetBytes(text);
        }
        catch (EncoderFallbackException)
        {
            throw new InvalidDataException("A file name in the package is not valid Unicode and cannot be signed.");
        }
    }

    /// <summary>Walks a folder without following links and adds a line for every file.</summary>
    /// <param name="directory">The folder to list.</param>
    /// <param name="root">The package root, which the paths are relative to.</param>
    /// <param name="lines">Receives the relative path and the hash of each file.</param>
    /// <exception cref="InvalidDataException">An entry is a link, or its name cannot be signed unambiguously.</exception>
    private static void Collect(DirectoryInfo directory, string root, List<(string Path, string Hash)> lines)
    {
        foreach (var entry in directory.EnumerateFileSystemInfos("*", ListEverything))
        {
            // A link (symlink or junction) is refused whatever it points at: its target is outside what the hash covers.
            if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint) || entry.LinkTarget is not null)
            {
                throw new InvalidDataException("The package holds a symbolic link or junction and cannot be signed.");
            }

            if (entry is DirectoryInfo child)
            {
                Collect(child, root, lines);
                continue;
            }

            var relative = Path.GetRelativePath(root, entry.FullName).Replace('\\', '/');
            if (string.Equals(relative, PluginSignatureFile.FileName, StringComparison.Ordinal))
            {
                continue;
            }

            if (relative.Any(char.IsControl))
            {
                throw new InvalidDataException("A file name in the package holds a control character and cannot be signed.");
            }

            lines.Add((relative, HashOf(entry.FullName)));
        }
    }

    /// <summary>Hashes one file without holding it in memory.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The lower-case hex SHA-256.</returns>
    private static string HashOf(string path)
    {
        // The walk has already refused every link, and a path swapped for a link between that check and this open
        // would be followed (the runtime offers no open option that refuses links on every platform). That swap needs
        // write access to the package folder. When the host verifies, it owns that folder: the package root is created by the host (a
        // staging folder it extracts the archive into, or an install folder only its own process writes), and a zip
        // entry is written as a regular file, never as a link. (The signing tool runs on the publisher's own folder.) Anyone who could swap a path there could already change
        // the files themselves, so a handle check would add nothing the folder's ownership does not already give.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
