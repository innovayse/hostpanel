using System.Formats.Tar;
using System.IO.Compression;
using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent;

/// <summary>
/// The one place an <see cref="ISshAgentBundle"/> is packed: entries are sorted and carry fixed times, owners and modes, so
/// the same files always produce the same bytes and the same SHA256, whichever profile the files belong to.
/// </summary>
internal static class SshAgentBundleBuilder
{
    /// <summary>Folder prefix of the helpers, the one place a program the agent runs may live.</summary>
    private const string HelperFolder = "providers/";

    /// <summary>Fixed modification time of every entry.</summary>
    private static readonly DateTimeOffset EntryTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The paths of the agent and its two scripts, which are installed as programs.</summary>
    private static readonly string[] Programs = ["hostpanel-ssh-key-agent", "install.sh", "uninstall.sh"];

    /// <summary>Packs the files into a gzip compressed tar whose entries all sit under one root folder named for the agent version.</summary>
    /// <param name="version">The agent version, which names the root folder.</param>
    /// <param name="files">The files to pack; their order does not matter.</param>
    /// <returns>The <c>.tar.gz</c> bytes.</returns>
    public static byte[] Build(string version, IEnumerable<SshAgentFile> files)
    {
        var root = $"hostpanel-ssh-key-agent-{version}/";
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
        {
            using var tar = new TarWriter(gzip, TarEntryFormat.Ustar, leaveOpen: true);
            foreach (var file in files.OrderBy(f => f.Path, StringComparer.Ordinal))
            {
                var entry = new UstarTarEntry(TarEntryType.RegularFile, root + file.Path)
                {
                    ModificationTime = EntryTime,
                    Mode = IsProgram(file.Path)
                        ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute
                        : UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead,
                    Uid = 0,
                    Gid = 0,
                    UserName = "root",
                    GroupName = "root",
                    DataStream = new MemoryStream(file.Bytes),
                };
                tar.WriteEntry(entry);
            }
        }

        return buffer.ToArray();
    }

    /// <summary>Tells whether a file is installed as a program: the agent, its scripts and every helper.</summary>
    /// <param name="path">The path inside the bundle.</param>
    /// <returns><see langword="true"/> when the file gets the executable mode.</returns>
    private static bool IsProgram(string path)
    {
        return Programs.Contains(path, StringComparer.Ordinal) || path.StartsWith(HelperFolder, StringComparison.Ordinal);
    }
}
