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
/// A path holding a control character (a line feed in a file name would let one file stand for two lines) cannot
/// be signed unambiguously and makes <see cref="Build"/> throw.
/// </para>
/// </remarks>
public static class PluginSignaturePayload
{
    /// <summary>The first line: names the format, so a later change to it gets a new name instead of a silent clash.</summary>
    public const string Header = "innovayse-plugin-signature-v1";

    /// <summary>The prefix of the key id line.</summary>
    private const string KeyLinePrefix = "key ";

    /// <summary>The buffer size used to hash a file.</summary>
    private const int BufferSize = 81_920;

    /// <summary>Builds the payload of the package in a folder.</summary>
    /// <param name="packageDir">The package root: the folder holding <c>plugin.json</c>.</param>
    /// <param name="keyId">The id of the key that signs, or is claimed to have signed.</param>
    /// <returns>The bytes that are signed.</returns>
    /// <exception cref="InvalidDataException">The key id is not valid, or a file name in the package cannot be signed unambiguously.</exception>
    public static byte[] Build(string packageDir, string keyId)
    {
        ArgumentException.ThrowIfNullOrEmpty(packageDir);
        if (!TrustedKeyId.IsValid(keyId))
        {
            throw new InvalidDataException("The key id is not a valid key id.");
        }

        var root = Path.GetFullPath(packageDir);
        var lines = new List<(string Path, string Hash)>();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (string.Equals(relative, PluginSignatureFile.FileName, StringComparison.Ordinal))
            {
                continue;
            }

            if (relative.Any(char.IsControl))
            {
                throw new InvalidDataException("A file name in the package holds a control character and cannot be signed.");
            }

            lines.Add((relative, HashOf(file)));
        }

        lines.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));

        var text = new StringBuilder()
            .Append(Header).Append('\n')
            .Append(KeyLinePrefix).Append(keyId).Append('\n');
        foreach (var (path, hash) in lines)
        {
            text.Append(hash).Append(' ').Append(path).Append('\n');
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text.ToString());
    }

    /// <summary>Hashes one file without holding it in memory.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The lower-case hex SHA-256.</returns>
    private static string HashOf(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
