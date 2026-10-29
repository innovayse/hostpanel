namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>Fetches the latest published release from a GitHub repository.</summary>
public interface IGitHubReleasesClient
{
    /// <summary>
    /// Returns the tag, publish date and HTML URL of the latest published (non-pre-release,
    /// non-draft) release of the Hostpanel repository, or <see langword="null"/> when GitHub
    /// reports no published release.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The release descriptor, or <see langword="null"/>.</returns>
    Task<GitHubReleaseInfo?> GetLatestAsync(CancellationToken ct);
}
