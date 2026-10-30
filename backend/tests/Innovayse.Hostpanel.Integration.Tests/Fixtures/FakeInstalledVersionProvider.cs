using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// In-memory replacement for <see cref="IInstalledVersionProvider"/> used by the integration test
/// host. Tests that exercise version-comparison logic set <see cref="Version"/> to a known value
/// so the results are deterministic and independent of the running assembly's attributes.
/// </summary>
public sealed class FakeInstalledVersionProvider : IInstalledVersionProvider
{
    /// <summary>
    /// The version string returned by <see cref="Version"/>. Defaults to <c>1.0.0</c> so tests
    /// that do not override it get a well-known, parseable baseline.
    /// </summary>
    public string Version { get; set; } = "1.0.0";
}
