using Innovayse.Hostpanel.Domain.Servers;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// The control panel modules the provisioning test doubles stand in for. The host builds cPanel only, so a test that needs a
/// second panel (two modules in use, a module filter) seeds its servers straight into the database on <see cref="Second"/>, the
/// way a panel plugin's module id would be stored.
/// </summary>
public static class TestPanelModules
{
    /// <summary>The id of a second panel, as an installed panel plugin would declare it.</summary>
    public const string Second = "test-panel";

    /// <summary>Every module the test doubles report as supported: the host's own plus <see cref="Second"/>.</summary>
    public static IReadOnlyCollection<string> Supported { get; } = Array.AsReadOnly([.. ServerModules.Known, Second]);
}
