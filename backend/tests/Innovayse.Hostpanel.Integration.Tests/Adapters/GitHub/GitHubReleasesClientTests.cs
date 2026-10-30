using Innovayse.Hostpanel.Infrastructure.Integrations.GitHub;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.GitHub;

/// <summary>
/// Unit tests for <see cref="GitHubReleasesClient"/> response parsing.
/// Uses a loopback <see cref="StubHttpServer"/> to control GitHub API responses without
/// any real outbound HTTP.
/// </summary>
public sealed class GitHubReleasesClientTests : IAsyncLifetime
{
    /// <summary>The loopback server standing in for the GitHub API.</summary>
    private StubHttpServer _github = null!;

    /// <summary>Starts the stub server before each test.</summary>
    /// <returns>A task that completes when the server is listening.</returns>
    public async Task InitializeAsync()
    {
        _github = await StubHttpServer.StartAsync();
    }

    /// <summary>Stops the stub server after each test.</summary>
    /// <returns>A task that completes when the server is stopped.</returns>
    public async Task DisposeAsync()
    {
        await _github.DisposeAsync();
    }

    /// <summary>Builds a <see cref="GitHubReleasesClient"/> aimed at the stub server.</summary>
    private GitHubReleasesClient BuildClient()
    {
        var http = new HttpClient { BaseAddress = _github.BaseAddress };
        var options = Options.Create(new ReleaseOptions());
        return new GitHubReleasesClient(http, options);
    }

    /// <summary>A published release with a standard tag returns a populated release info record.</summary>
    [Fact]
    public async Task GetLatestAsync_PublishedRelease_ReturnsParsedInfo()
    {
        _github.Enqueue("""
            {
                "tag_name": "v1.5.0",
                "html_url": "https://github.com/innovayse/hostpanel/releases/tag/v1.5.0",
                "published_at": "2026-06-01T00:00:00Z",
                "prerelease": false,
                "draft": false
            }
            """);

        var result = await BuildClient().GetLatestAsync(CancellationToken.None);

        result.Should().NotBeNull();
        result!.Version.Should().Be("1.5.0");
        result.ReleaseNotesUrl.Should().Contain("v1.5.0");
    }

    /// <summary>The leading <c>v</c> is stripped from the tag name.</summary>
    [Fact]
    public async Task GetLatestAsync_TagWithLeadingV_StripsOnlyOneV()
    {
        _github.Enqueue("""
            {
                "tag_name": "v2.0.0",
                "html_url": "https://github.com/innovayse/hostpanel/releases/tag/v2.0.0",
                "published_at": "2026-09-01T00:00:00Z",
                "prerelease": false,
                "draft": false
            }
            """);

        var result = await BuildClient().GetLatestAsync(CancellationToken.None);

        result!.Version.Should().Be("2.0.0");
    }

    /// <summary>A tag without a leading <c>v</c> is returned unchanged.</summary>
    [Fact]
    public async Task GetLatestAsync_TagWithoutLeadingV_ReturnsTagAsIs()
    {
        _github.Enqueue("""
            {
                "tag_name": "3.1.0",
                "html_url": "https://github.com/innovayse/hostpanel/releases/tag/3.1.0",
                "published_at": "2026-09-01T00:00:00Z",
                "prerelease": false,
                "draft": false
            }
            """);

        var result = await BuildClient().GetLatestAsync(CancellationToken.None);

        result!.Version.Should().Be("3.1.0");
    }

    /// <summary>A draft release is ignored and <see langword="null"/> is returned.</summary>
    [Fact]
    public async Task GetLatestAsync_DraftRelease_ReturnsNull()
    {
        _github.Enqueue("""
            {
                "tag_name": "v1.5.0",
                "html_url": "https://github.com/innovayse/hostpanel/releases/tag/v1.5.0",
                "published_at": "2026-06-01T00:00:00Z",
                "prerelease": false,
                "draft": true
            }
            """);

        var result = await BuildClient().GetLatestAsync(CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>A pre-release is ignored and <see langword="null"/> is returned.</summary>
    [Fact]
    public async Task GetLatestAsync_Prerelease_ReturnsNull()
    {
        _github.Enqueue("""
            {
                "tag_name": "v1.5.0-rc1",
                "html_url": "https://github.com/innovayse/hostpanel/releases/tag/v1.5.0-rc1",
                "published_at": "2026-06-01T00:00:00Z",
                "prerelease": true,
                "draft": false
            }
            """);

        var result = await BuildClient().GetLatestAsync(CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>A release with an empty tag name is ignored and <see langword="null"/> is returned.</summary>
    [Fact]
    public async Task GetLatestAsync_EmptyTag_ReturnsNull()
    {
        _github.Enqueue("""
            {
                "tag_name": "",
                "html_url": "https://github.com/innovayse/hostpanel/releases/tag/",
                "published_at": "2026-06-01T00:00:00Z",
                "prerelease": false,
                "draft": false
            }
            """);

        var result = await BuildClient().GetLatestAsync(CancellationToken.None);

        result.Should().BeNull();
    }
}
