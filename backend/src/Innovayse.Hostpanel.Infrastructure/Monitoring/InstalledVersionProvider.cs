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
    public string Version { get; } = ReadVersion();

    /// <summary>Reads the informational version from the entry assembly at startup.</summary>
    /// <returns>The version string without metadata suffix, or <c>dev</c> when absent.</returns>
    private static string ReadVersion()
    {
        var raw = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

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
