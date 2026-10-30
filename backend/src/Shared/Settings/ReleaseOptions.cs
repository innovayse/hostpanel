using System.ComponentModel.DataAnnotations;

namespace Innovayse.Hostpanel.Shared.Settings;

/// <summary>
/// Controls where release-update checks point and what upgrade command is shown. Bound from the
/// <c>Release</c> section; every value has a safe default that targets the official repository.
/// A fork or an air-gapped deployment can override all three without touching application code.
/// </summary>
public sealed class ReleaseOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "Release";

    /// <summary>Base address of the GitHub REST API, including the trailing slash.</summary>
    [Required]
    [Url]
    public string GitHubApiBaseUrl { get; init; } = "https://api.github.com/";

    /// <summary>
    /// The <c>owner/repo</c> path used to build the releases endpoint, e.g.
    /// <c>repos/{Repository}/releases/latest</c>.
    /// </summary>
    [Required]
    public string Repository { get; init; } = "innovayse/hostpanel";

    /// <summary>
    /// URL of the install script streamed into <c>sh</c> for the upgrade command shown in the
    /// admin panel. The version placeholder <c>{0}</c> is appended as the <c>--version</c>
    /// argument.
    /// </summary>
    [Required]
    [Url]
    public string InstallScriptUrl { get; init; } =
        "https://raw.githubusercontent.com/innovayse/hostpanel/main/install.sh";
}
