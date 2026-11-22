using Innovayse.Hostpanel.Application.Admin.Plugins.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <see cref="PluginSdkCompatibility"/>, the one answer the upload, the registry install, the loader and the update
/// check
/// share to "can this host load a plugin built on that SDK": a newer SDK is refused, an equal or older one and one that
/// is
/// not stated pass, a stated value that is not a version is not stated. No container or database.
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
    [InlineData("3.11.0", "three", true)]
    [InlineData("3.11.0", "1.0", true)]
    [InlineData("local", "9.0.0", true)]
    [InlineData("", "9.0.0", true)]
    public void IsSupported_FollowsTheSemanticVersionOrder(string host, string? plugin, bool expected)
    {
        PluginSdkCompatibility.IsSupported(host, plugin).Should().Be(expected);
    }
}
