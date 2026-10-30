namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Returns the version of the currently running Hostpanel build, read from the entry assembly's
/// informational version attribute so it matches the release tag exactly.
/// </summary>
public interface IInstalledVersionProvider
{
    /// <summary>
    /// Gets the installed version string, e.g. <c>1.4.0</c>. Returns <c>dev</c> for local
    /// builds that carry no informational version.
    /// </summary>
    string Version { get; }
}
