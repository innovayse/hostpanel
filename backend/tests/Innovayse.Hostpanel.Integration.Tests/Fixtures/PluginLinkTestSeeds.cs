using System.Diagnostics;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Creates links inside a plugin package folder for the tests that prove links are refused. A link that was not
/// created must never let a test pass: every helper either returns a link that is checked to exist, or throws.
/// </summary>
internal static class PluginLinkTestSeeds
{
    /// <summary>The environment variable continuous integration sets; a link that cannot be made there fails the test.</summary>
    private const string CiVariable = "CI";

    /// <summary>
    /// Creates a directory link: a symbolic link on Unix, a junction on Windows (a junction needs no privilege, so
    /// this works on every developer machine).
    /// </summary>
    /// <param name="link">The path of the link to create.</param>
    /// <param name="target">The existing folder it points at.</param>
    /// <exception cref="InvalidOperationException">The link could not be created or is not seen as a link afterwards.</exception>
    public static void CreateDirectoryLink(string link, string target)
    {
        if (OperatingSystem.IsWindows())
        {
            CreateJunction(link, target);
        }
        else
        {
            Directory.CreateSymbolicLink(link, target);
        }

        AssertIsLink(new DirectoryInfo(link));
    }

    /// <summary>
    /// Creates a file symbolic link. On Unix a failure is an exception. On Windows a symbolic link needs developer
    /// mode or elevation: when the account cannot make one the test cannot run there, which is fatal when the
    /// <c>CI</c> environment variable is set and reported as <see langword="false"/> otherwise.
    /// </summary>
    /// <param name="link">The path of the link to create.</param>
    /// <param name="target">The existing file it points at.</param>
    /// <returns><see langword="true"/> when the link exists; <see langword="false"/> only on a local Windows account that may not create it.</returns>
    /// <exception cref="InvalidOperationException">The link could not be created in an environment that must support it.</exception>
    public static bool TryCreateFileLink(string link, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.CreateSymbolicLink(link, target);
            AssertIsLink(new FileInfo(link));
            return true;
        }

        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(CiVariable)))
            {
                throw new InvalidOperationException("The CI environment must be able to create a file symbolic link.", ex);
            }

            return false;
        }

        AssertIsLink(new FileInfo(link));
        return true;
    }

    /// <summary>Creates a directory junction with <c>mklink /J</c> through the command interpreter.</summary>
    /// <param name="link">The path of the junction.</param>
    /// <param name="target">The existing folder it points at.</param>
    /// <exception cref="InvalidOperationException">The command failed.</exception>
    private static void CreateJunction(string link, string target)
    {
        var start = new ProcessStartInfo("cmd.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("/c");
        start.ArgumentList.Add("mklink");
        start.ArgumentList.Add("/J");
        start.ArgumentList.Add(link);
        start.ArgumentList.Add(target);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("cmd.exe did not start.");
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"mklink /J failed: {output}");
        }
    }

    /// <summary>Asserts the entry really is a link, so a test cannot pass on a link that was never made.</summary>
    /// <param name="entry">The entry that should be a link.</param>
    /// <exception cref="InvalidOperationException">The entry is neither a reparse point nor has a link target.</exception>
    private static void AssertIsLink(FileSystemInfo entry)
    {
        if (entry.LinkTarget is null && !entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidOperationException($"'{entry.FullName}' was expected to be a link and is not.");
        }
    }
}
