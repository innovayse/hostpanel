using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Admin;

/// <summary>
/// The cached result of the most recent GitHub release check. A single row is kept in the
/// database so the API never queries GitHub on page loads; the daily cron updates it.
/// </summary>
/// <remarks>
/// Only one row exists. Handlers read it with <c>SingleOrDefaultAsync</c> and call
/// <see cref="Update"/> when the cron fetches a newer version; if the row does not exist yet
/// the cron inserts it via <see cref="Create"/>.
/// </remarks>
public sealed class LatestRelease : BaseEntity
{
    /// <summary>The fixed primary key of the singleton row.</summary>
    public const int SingletonId = 1;

    /// <summary>Gets the version tag published on GitHub, e.g. <c>1.5.0</c>.</summary>
    public string Version { get; private set; } = string.Empty;

    /// <summary>Gets when the release was published on GitHub.</summary>
    public DateTimeOffset PublishedAt { get; private set; }

    /// <summary>Gets the URL of the release notes page on GitHub.</summary>
    public string ReleaseNotesUrl { get; private set; } = string.Empty;

    /// <summary>Gets when the cron last successfully queried GitHub.</summary>
    public DateTimeOffset CheckedAt { get; private set; }

    /// <summary>EF Core parameterless constructor; do not call directly.</summary>
    private LatestRelease() { }

    /// <summary>Creates the singleton row after the first successful cron run.</summary>
    /// <param name="version">The version tag returned by GitHub, e.g. <c>1.5.0</c>.</param>
    /// <param name="publishedAt">When the release was published on GitHub.</param>
    /// <param name="releaseNotesUrl">URL of the GitHub release page.</param>
    /// <param name="checkedAt">The moment the cron ran.</param>
    /// <returns>A new, unsaved <see cref="LatestRelease"/> with <see cref="BaseEntity.Id"/> set to <see cref="SingletonId"/>.</returns>
    public static LatestRelease Create(
        string version,
        DateTimeOffset publishedAt,
        string releaseNotesUrl,
        DateTimeOffset checkedAt)
    {
        return new LatestRelease
        {
            Id = SingletonId,
            Version = version,
            PublishedAt = publishedAt,
            ReleaseNotesUrl = releaseNotesUrl,
            CheckedAt = checkedAt,
        };
    }

    /// <summary>Overwrites the cached release data after a successful cron run.</summary>
    /// <param name="version">The version tag returned by GitHub, e.g. <c>1.5.0</c>.</param>
    /// <param name="publishedAt">When the release was published on GitHub.</param>
    /// <param name="releaseNotesUrl">URL of the GitHub release page.</param>
    /// <param name="checkedAt">The moment the cron ran.</param>
    public void Update(
        string version,
        DateTimeOffset publishedAt,
        string releaseNotesUrl,
        DateTimeOffset checkedAt)
    {
        Version = version;
        PublishedAt = publishedAt;
        ReleaseNotesUrl = releaseNotesUrl;
        CheckedAt = checkedAt;
    }
}
