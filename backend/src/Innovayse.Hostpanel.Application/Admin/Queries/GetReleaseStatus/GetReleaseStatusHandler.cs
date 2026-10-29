using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Admin;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Queries.GetReleaseStatus;

/// <summary>
/// Handles <see cref="GetReleaseStatusQuery"/> by reading the installed version from
/// <see cref="IInstalledVersionProvider"/> and the cached GitHub release from the database.
/// No outbound HTTP call is made on this path.
/// </summary>
public sealed class GetReleaseStatusHandler
{
    /// <summary>Upgrade command template; the placeholder is replaced with the target version.</summary>
    private const string UpgradeCommandTemplate = "curl -fsSL https://raw.githubusercontent.com/innovayse/hostpanel/main/install.sh | sh -s -- --version {0}";

    /// <summary>The data context, for the cached latest release row.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Returns the version of the running build.</summary>
    private readonly IInstalledVersionProvider _versionProvider;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context, for the cached latest release row.</param>
    /// <param name="versionProvider">Returns the version of the running build.</param>
    public GetReleaseStatusHandler(IHostpanelDbContext context, IInstalledVersionProvider versionProvider)
    {
        _context = context;
        _versionProvider = versionProvider;
    }

    /// <summary>Reads the release status and returns a DTO safe to send to the admin panel.</summary>
    /// <param name="query">The query (no parameters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the release status. This use case refuses nothing.</returns>
    public async Task<Result<ReleaseStatusDto>> HandleAsync(GetReleaseStatusQuery query, CancellationToken ct)
    {
        _ = query;

        var installed = _versionProvider.Version;

        var latest = await _context.LatestReleases
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == LatestRelease.SingletonId, ct);

        if (latest is null)
        {
            return Result<ReleaseStatusDto>.Success(new ReleaseStatusDto(
                installed, null, null, null, null, false));
        }

        var isUpgrade = IsNewerVersion(installed, latest.Version);
        var upgradeCommand = isUpgrade
            ? string.Format(UpgradeCommandTemplate, latest.Version)
            : null;

        return Result<ReleaseStatusDto>.Success(new ReleaseStatusDto(
            installed,
            latest.Version,
            latest.PublishedAt,
            latest.ReleaseNotesUrl,
            upgradeCommand,
            isUpgrade));
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="candidate"/> is strictly newer than
    /// <paramref name="installed"/>. Unparseable versions are never considered upgrades.
    /// </summary>
    /// <param name="installed">The version string of the running build.</param>
    /// <param name="candidate">The version string of the GitHub release.</param>
    /// <returns><see langword="true"/> when the candidate is a newer semantic version.</returns>
    private static bool IsNewerVersion(string installed, string candidate)
    {
        if (!Version.TryParse(installed, out var installedVersion) ||
            !Version.TryParse(candidate, out var candidateVersion))
        {
            return false;
        }

        return candidateVersion > installedVersion;
    }
}
