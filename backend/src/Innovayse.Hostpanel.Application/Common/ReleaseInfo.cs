namespace Innovayse.Hostpanel.Application.Common;

/// <summary>The data returned by a release feed for one published release.</summary>
/// <param name="Version">Version tag without the leading <c>v</c>, e.g. <c>1.5.0</c>.</param>
/// <param name="PublishedAt">When the release was published.</param>
/// <param name="ReleaseNotesUrl">HTML URL of the release notes page.</param>
public sealed record ReleaseInfo(string Version, DateTimeOffset PublishedAt, string ReleaseNotesUrl);
