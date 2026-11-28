namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>Locates the backend solution directory the source scans read from.</summary>
internal static class BackendLayout
{
    /// <summary>
    /// Walks up from a build output directory to the folder that holds the solution file, so a scan
    /// finds the repository layout regardless of which configuration produced the test binary.
    /// </summary>
    /// <param name="startDirectory">The directory to start the upward walk from.</param>
    /// <returns>The directory containing the solution file.</returns>
    internal static string FindRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);

        while (current is not null)
        {
            if (current.GetFiles("*.sln").Length > 0)
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException(
            $"No solution file found walking up from '{startDirectory}'.");
    }
}
