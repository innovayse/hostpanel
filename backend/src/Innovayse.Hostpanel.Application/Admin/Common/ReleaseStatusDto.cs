namespace Innovayse.Hostpanel.Application.Admin.Common;

/// <summary>
/// The version status returned to the admin panel: the installed version, the latest known
/// release from GitHub, and whether an upgrade is available.
/// </summary>
/// <param name="InstalledVersion">The assembly informational version of the running build, e.g. <c>1.4.0</c>.</param>
/// <param name="LatestVersion">
/// The latest published GitHub release version, e.g. <c>1.5.0</c>; <see langword="null"/> when the
/// cron has not yet run or release checks are disabled.
/// </param>
/// <param name="LatestPublishedAt">
/// When the latest release was published on GitHub; <see langword="null"/> when <paramref name="LatestVersion"/> is.
/// </param>
/// <param name="ReleaseNotesUrl">
/// URL of the latest release page; <see langword="null"/> when <paramref name="LatestVersion"/> is.
/// </param>
/// <param name="UpgradeCommand">
/// The shell command the operator should run to upgrade, e.g.
/// <c>curl -fsSL https://… | sh -s -- --version 1.5.0</c>; <see langword="null"/> when no upgrade is available.
/// </param>
/// <param name="IsUpgradeAvailable">
/// <see langword="true"/> when <paramref name="LatestVersion"/> is newer than <paramref name="InstalledVersion"/>.
/// </param>
public sealed record ReleaseStatusDto(
    string InstalledVersion,
    string? LatestVersion,
    DateTimeOffset? LatestPublishedAt,
    string? ReleaseNotesUrl,
    string? UpgradeCommand,
    bool IsUpgradeAvailable);
