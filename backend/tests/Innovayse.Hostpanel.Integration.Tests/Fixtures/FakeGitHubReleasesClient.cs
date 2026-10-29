using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// In-memory replacement for <see cref="IGitHubReleasesClient"/> used by the integration test
/// host. Tests configure the return value before exercising the handler or endpoint under test.
/// </summary>
public sealed class FakeGitHubReleasesClient : IGitHubReleasesClient
{
    /// <summary>
    /// The value <see cref="GetLatestAsync"/> returns. Set to a <see cref="GitHubReleaseInfo"/>
    /// to simulate a successful fetch, or leave <see langword="null"/> to simulate GitHub
    /// returning no published release.
    /// </summary>
    public GitHubReleaseInfo? Next { get; set; }

    /// <summary>
    /// When <see langword="true"/>, <see cref="GetLatestAsync"/> throws
    /// <see cref="HttpRequestException"/> to simulate a network failure.
    /// </summary>
    public bool ShouldFail { get; set; }

    /// <summary>Number of times <see cref="GetLatestAsync"/> was called.</summary>
    public int CallCount { get; private set; }

    /// <inheritdoc/>
    public Task<GitHubReleaseInfo?> GetLatestAsync(CancellationToken ct)
    {
        CallCount++;
        if (ShouldFail)
        {
            throw new HttpRequestException("Simulated GitHub API failure.");
        }

        return Task.FromResult(Next);
    }
}
