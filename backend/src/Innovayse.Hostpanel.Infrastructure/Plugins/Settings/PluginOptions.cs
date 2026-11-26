namespace Innovayse.Hostpanel.Infrastructure.Plugins.Settings;

/// <summary>
/// Where uploaded plugins live and what an uploaded plugin archive may be, bound from the
/// <c>Plugins</c> section. Every key is optional; the defaults suit a single-server install.
/// </summary>
/// <remarks>
/// Two plugin folders exist. The plugins shipped with the build sit in <see cref="BuiltInRoot"/>
/// (the build output's <c>plugins/</c> folder), are loaded first and can never be removed or
/// replaced from the admin panel. Uploaded plugins are unpacked into <see cref="InstallRoot"/>,
/// which must be a different folder; mount it on a volume to keep uploads across redeploys.
/// </remarks>
public sealed class PluginOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Plugins";

    /// <summary>The folder name, under the application folder, used when <see cref="InstallPath"/> is not set.</summary>
    public const string DefaultInstallFolder = "plugins-installed";

    /// <summary>
    /// The highest <see cref="MaxViewBytes"/> an operator may set: 4 MiB, in bytes. Every view document of every loaded
    /// plugin is held in memory, and a page request copies its document once per language, so a view larger than this
    /// is a memory problem rather than a page.
    /// </summary>
    public const long MaxViewBytesCeiling = 4L * 1024 * 1024;

    /// <summary>The default <see cref="MaxViewBytes"/>: 256 KiB, in bytes, far above any hand-written view.</summary>
    public const long DefaultMaxViewBytes = 256L * 1024;

    /// <summary>
    /// The folder uploaded plugins are unpacked into, absolute or relative to the application
    /// folder. Empty means <see cref="DefaultInstallFolder"/> under the application folder.
    /// </summary>
    public string InstallPath { get; set; } = string.Empty;

    /// <summary>The most entries (files and folders) an uploaded archive may hold.</summary>
    public int MaxEntryCount { get; set; } = 1_000;

    /// <summary>The most bytes an uploaded archive may unpack to, counted as it is unpacked.</summary>
    public long MaxUncompressedBytes { get; set; } = 200L * 1024 * 1024;

    /// <summary>
    /// The most bytes one plugin view document (the <c>view</c> file of a page or slot) may hold on disk, in bytes; a
    /// larger file refuses the plugin at load and at install. 1 to <see cref="MaxViewBytesCeiling"/>.
    /// </summary>
    public long MaxViewBytes { get; set; } = DefaultMaxViewBytes;

    /// <summary>
    /// The SHA-256 digests (64 hex characters) of the plugin archives this host accepts. Empty, the
    /// default, accepts any archive that passes the other checks; once one digest is listed, an
    /// archive whose digest is not listed is refused.
    /// </summary>
    public List<string> TrustedPackageSha256 { get; set; } = [];

    /// <summary>The longest, in seconds, a plugin's migration hook may run before the host stops waiting for it.</summary>
    public const int DefaultMigrationTimeoutSeconds = 120;

    /// <summary>The upper bound <see cref="MigrationTimeoutSeconds"/> accepts: a migration that needs longer is not a startup hook.</summary>
    public const int MaxMigrationTimeoutSeconds = 3_600;

    /// <summary>
    /// The longest, in seconds, one plugin's migration hook may run. Past it the host cancels the hook, stops waiting,
    /// logs the plugin and keeps it gated until a later attempt succeeds, so a hanging hook cannot hold up startup or a
    /// settings save. Must be between 1 and <see cref="MaxMigrationTimeoutSeconds"/>.
    /// </summary>
    public int MigrationTimeoutSeconds { get; set; } = DefaultMigrationTimeoutSeconds;

    /// <summary>The default <see cref="DrainSeconds"/>: long enough for an ordinary request, short enough for a restart to stay a pause.</summary>
    public const int DefaultDrainSeconds = 30;

    /// <summary>The upper bound <see cref="DrainSeconds"/> accepts: a drain longer than this is a stuck request, not a pause.</summary>
    public const int MaxDrainSeconds = 300;

    /// <summary>
    /// The longest, in seconds, the host lets running requests finish when it stops for a plugin restart, and the longest a
    /// plugin reload waits for the calls still running in the old version before it gives up and leaves the change to a
    /// restart. It is also the host's shutdown timeout, so the container's stop grace period must be larger. Must be
    /// between 1 and <see cref="MaxDrainSeconds"/>.
    /// </summary>
    public int DrainSeconds { get; set; } = DefaultDrainSeconds;

    /// <summary>The default <see cref="RestartDelaySeconds"/>.</summary>
    public const int DefaultRestartDelaySeconds = 3;

    /// <summary>The upper bound <see cref="RestartDelaySeconds"/> accepts.</summary>
    public const int MaxRestartDelaySeconds = 60;

    /// <summary>
    /// The pause, in seconds, between a request for a restart and the start of the host's stop. It lets the request that asked
    /// for the restart finish, and lets plugin changes made one after another in that time be applied by the same restart.
    /// Must be between 0 and <see cref="MaxRestartDelaySeconds"/>.
    /// </summary>
    public int RestartDelaySeconds { get; set; } = DefaultRestartDelaySeconds;

    /// <summary>The build output's plugin folder, holding the plugins shipped with the build.</summary>
    public static string BuiltInRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "plugins"));

    /// <summary>The resolved, absolute folder uploaded plugins are unpacked into.</summary>
    public string InstallRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        string.IsNullOrWhiteSpace(InstallPath) ? DefaultInstallFolder : InstallPath));
}
