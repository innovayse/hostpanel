using System.Text.Json.Serialization;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.GitHub;

/// <summary>JSON wire model for the GitHub Releases API <c>GET /repos/{owner}/{repo}/releases/latest</c> response.</summary>
internal sealed class GitHubReleaseWire
{
    /// <summary>Gets or sets the release tag, e.g. <c>v1.5.0</c>.</summary>
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    /// <summary>Gets or sets the HTML URL of the release page.</summary>
    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets when the release was published.</summary>
    [JsonPropertyName("published_at")]
    public DateTimeOffset PublishedAt { get; set; }

    /// <summary>Gets or sets whether this is a pre-release; pre-releases are ignored.</summary>
    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }

    /// <summary>Gets or sets whether this is a draft; drafts are ignored.</summary>
    [JsonPropertyName("draft")]
    public bool Draft { get; set; }
}
