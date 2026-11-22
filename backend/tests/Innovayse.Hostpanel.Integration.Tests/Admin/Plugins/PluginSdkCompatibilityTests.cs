using Innovayse.Hostpanel.Application.Admin.Plugins.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <see cref="PluginSdkCompatibility"/>, the one answer the upload, the registry install, the loader and the update check
/// share to "can this host load a plugin built on that SDK": a newer SDK is refused, an equal or older one and one that is
/// not stated pass, a stated value that is not a version fails closed. No container or database.
/// </summary>
public sealed class PluginSdkCompatibilityTests
{
    /// <summary>Each host SDK and plugin SDK answers as the semantic version order says.</summary>
    /// <param name="host">The SDK version the host ships.</param>
    /// <param name="plugin">The SDK version the plugin was built on, or <see langword="null"/>.</param>
    /// <param name="expected">Whether the host can load the plugin.</param>
    [Theory]
    [InlineData("3.11.0", "3.11.0", true)]
    [InlineData("3.11.0", "3.10.0", true)]
    [InlineData("3.11.0", "3.11.1", false)]
    [InlineData("3.11.0", "3.12.0", false)]
    [InlineData("3.11.0", "4.0.0", false)]
    [InlineData("3.11.0", null, true)]
    [InlineData("3.11.0", "", true)]
    [InlineData("3.11.0", "three", false)]
    [InlineData("local", "9.0.0", true)]
    public void IsSupported_FollowsTheSemanticVersionOrder(string host, string? plugin, bool expected)
    {
        PluginSdkCompatibility.IsSupported(host, plugin).Should().Be(expected);
    }

    /// <summary>The host's own SDK version is a version, and a plugin built on it is supported while the next minor is not.</summary>
    [Fact]
    public void IsSupported_AgainstTheShippedSdk_AcceptsItAndRefusesTheNextMinor()
    {
        var parts = PluginSdkCompatibility.HostSdkVersion.Split('.');
        parts.Should().HaveCount(3);

        PluginSdkCompatibility.IsSupported(PluginSdkCompatibility.HostSdkVersion).Should().BeTrue();
        PluginSdkCompatibility.IsSupported($"{parts[0]}.{int.Parse(parts[1]) + 1}.0").Should().BeFalse();
    }
}
