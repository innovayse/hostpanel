namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>Fetches the latest published release from a remote release feed.</summary>
public interface IReleaseFeed
{
    /// <summary>
    /// Returns the tag, publish date and HTML URL of the latest published (non-pre-release,
    /// non-draft) release, or <see langword="null"/> when the feed reports no published release.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The release descriptor, or <see langword="null"/>.</returns>
    Task<ReleaseInfo?> GetLatestAsync(CancellationToken ct);
}
