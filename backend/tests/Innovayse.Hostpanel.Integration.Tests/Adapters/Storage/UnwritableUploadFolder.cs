namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Storage;

/// <summary>
/// A temporary web root whose upload folder the current process may not write to, standing in for a volume that an
/// older image created as root. Disposing it restores the mode and deletes the folder.
/// </summary>
public sealed class UnwritableUploadFolder : IDisposable
{
    /// <summary>Initializes the folder, read-only for the current user.</summary>
    public UnwritableUploadFolder()
    {
        WebRoot = Path.Combine(Path.GetTempPath(), $"hostpanel-unwritable-{Guid.NewGuid():n}");
        Directory.CreateDirectory(Path.Combine(LogoDirectory, ExistingUploadId));
        if (!OperatingSystem.IsWindows() && CanBeMadeUnwritable)
        {
            File.SetUnixFileMode(LogoDirectory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            File.SetUnixFileMode(BrandingDirectory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }
    }

    /// <summary>
    /// Gets whether a permission change can deny this process a write: not on Windows (no Unix modes) and not as root
    /// or an administrator, who bypass them, so a test that needs a refusal returns early where it cannot be produced.
    /// </summary>
    public static bool CanBeMadeUnwritable => !OperatingSystem.IsWindows() && !Environment.IsPrivilegedProcess;

    /// <summary>Gets the id of an upload directory that already exists under the read-only logo directory.</summary>
    public string ExistingUploadId { get; } = Guid.NewGuid().ToString("n");

    /// <summary>Gets the directory that stands in for the web root.</summary>
    public string WebRoot { get; }

    /// <summary>Gets the branding upload directory, the one made read-only.</summary>
    private string BrandingDirectory => Path.Combine(WebRoot, "uploads", "branding");

    /// <summary>Gets the directory of the light logo uploads, read-only like its parent.</summary>
    private string LogoDirectory => Path.Combine(BrandingDirectory, "logo");

    /// <inheritdoc />
    public void Dispose()
    {
        if (!OperatingSystem.IsWindows() && CanBeMadeUnwritable)
        {
            File.SetUnixFileMode(BrandingDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.SetUnixFileMode(LogoDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Directory.Delete(WebRoot, recursive: true);
    }
}
