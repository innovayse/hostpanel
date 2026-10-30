namespace Innovayse.Hostpanel.Application.Admin.Queries.GetReleaseStatus;

/// <summary>
/// Returns the installed Hostpanel version, the latest known release from GitHub, and whether
/// an upgrade is available. The latest release data comes from the database row kept by
/// <c>CheckReleaseUpdatesCron</c>; no outbound HTTP call is made on this path.
/// </summary>
public sealed record GetReleaseStatusQuery;
