using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.GitHub;

/// <summary>
/// Fetches the latest published Hostpanel release from the GitHub Releases API.
/// Registered as a typed <see cref="HttpClient"/> by <c>InfrastructureDependencyInjection</c>.
/// </summary>
public sealed class GitHubReleasesClient : IGitHubReleasesClient
{
    /// <summary>The typed <see cref="HttpClient"/> pre-configured with the GitHub API base address and headers.</summary>
    private readonly HttpClient _http;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<GitHubReleasesClient> _logger;

    /// <summary>Initializes a new instance of the <see cref="GitHubReleasesClient"/> class.</summary>
    /// <param name="http">The typed <see cref="HttpClient"/> pre-configured with the GitHub API base address and headers.</param>
    /// <param name="logger">Structured logger.</param>
    public GitHubReleasesClient(HttpClient http, ILogger<GitHubReleasesClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<GitHubReleaseInfo?> GetLatestAsync(CancellationToken ct)
    {
        try
        {
            var wire = await _http.GetFromJsonAsync<GitHubReleaseWire>(
                "repos/innovayse/hostpanel/releases/latest", ct);

            if (wire is null || wire.Draft || wire.Prerelease || string.IsNullOrWhiteSpace(wire.TagName))
            {
                return null;
            }

            // Strip the leading 'v' that GitHub adds to tags, e.g. v1.5.0 → 1.5.0.
            var version = wire.TagName.TrimStart('v');

            return new GitHubReleaseInfo(version, wire.PublishedAt, wire.HtmlUrl);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to fetch the latest Hostpanel release from GitHub.");
            throw;
        }
    }
}
