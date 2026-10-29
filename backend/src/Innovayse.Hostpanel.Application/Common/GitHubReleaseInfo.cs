namespace Innovayse.Hostpanel.Application.Common;

/// <summary>The data returned by the GitHub Releases API for one published release.</summary>
/// <param name="Version">Version tag without the leading <c>v</c>, e.g. <c>1.5.0</c>.</param>
/// <param name="PublishedAt">When the release was published.</param>
/// <param name="ReleaseNotesUrl">HTML URL of the GitHub release page.</param>
public sealed record GitHubReleaseInfo(string Version, DateTimeOffset PublishedAt, string ReleaseNotesUrl);
