using System.Reflection;
using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Infrastructure.Monitoring;

/// <summary>
/// Reads the installed Hostpanel version from the entry assembly's informational version
/// attribute, which is set to the release tag by the CI build. Returns <c>dev</c> for local
/// builds that carry no informational version.
/// </summary>
public sealed class InstalledVersionProvider : IInstalledVersionProvider
{
    /// <summary>Fallback version string for local or untagged builds.</summary>
    private const string DevVersion = "dev";

    /// <inheritdoc/>
    public string Version { get; } = ParseVersion(
        Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion);

    /// <summary>
    /// Parses the raw informational version string from an assembly attribute into a clean version
    /// string, stripping the <c>+&lt;commit&gt;</c> suffix that the SDK appends.
    /// </summary>
    /// <param name="raw">The raw attribute value, e.g. <c>1.4.0+abc1234</c>.</param>
    /// <returns>The version without a metadata suffix, or <c>dev</c> when the input is absent.</returns>
    internal static string ParseVersion(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DevVersion;
        }

        // Strip the '+<commit>' suffix that dotnet appends to informational versions
        // (e.g. "1.4.0+abc1234" → "1.4.0").
        var plusIndex = raw.IndexOf('+', StringComparison.Ordinal);
        return plusIndex >= 0 ? raw[..plusIndex] : raw;
    }
}
