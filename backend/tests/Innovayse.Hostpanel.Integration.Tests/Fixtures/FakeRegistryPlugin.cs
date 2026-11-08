namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>A plugin version the fake plugin registry offers.</summary>
public sealed class FakeRegistryPlugin
{
    /// <summary>Gets the plugin id.</summary>
    public required string PluginId { get; init; }

    /// <summary>Gets the version.</summary>
    public required string Version { get; init; }

    /// <summary>Gets the package bytes.</summary>
    public required byte[] Package { get; init; }

    /// <summary>Gets whether the plugin is distributed to verified installations only.</summary>
    public bool HostOnly { get; init; }

    /// <summary>Gets the lower-case hex SHA-256 the registry publishes; the real one unless a test sets another.</summary>
    public string Sha256 { get; init; } = string.Empty;
}
