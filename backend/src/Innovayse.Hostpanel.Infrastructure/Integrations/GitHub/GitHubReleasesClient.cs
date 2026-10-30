using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.GitHub;

/// <summary>
/// Fetches the latest published Hostpanel release from the GitHub Releases API.
/// Registered as a typed <see cref="HttpClient"/> by <c>InfrastructureDependencyInjection</c>.
/// </summary>
public sealed class GitHubReleasesClient : IReleaseFeed
{
    /// <summary>The typed <see cref="HttpClient"/> pre-configured with the GitHub API base address and headers.</summary>
    private readonly HttpClient _http;

    /// <summary>Release configuration: which repository to query.</summary>
    private readonly ReleaseOptions _options;

    /// <summary>Initializes a new instance of the <see cref="GitHubReleasesClient"/> class.</summary>
    /// <param name="http">The typed <see cref="HttpClient"/> pre-configured with the GitHub API base address and headers.</param>
    /// <param name="options">Release configuration, supplying the repository path.</param>
    public GitHubReleasesClient(HttpClient http, IOptions<ReleaseOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async Task<ReleaseInfo?> GetLatestAsync(CancellationToken ct)
    {
        var wire = await _http.GetFromJsonAsync<GitHubReleaseWire>(
            $"repos/{_options.Repository}/releases/latest", ct);

        if (wire is null || wire.Draft || wire.Prerelease || string.IsNullOrWhiteSpace(wire.TagName))
        {
            return null;
        }

        // Strip the single leading 'v' that GitHub adds to tags, e.g. v1.5.0 → 1.5.0.
        var version = wire.TagName.StartsWith('v') ? wire.TagName[1..] : wire.TagName;

        return new ReleaseInfo(version, wire.PublishedAt, wire.HtmlUrl);
    }
}
