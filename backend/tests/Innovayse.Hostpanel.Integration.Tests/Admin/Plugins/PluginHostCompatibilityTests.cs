using Innovayse.Hostpanel.Application.Admin.Plugins.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <see cref="PluginHostCompatibility"/>, the one range check the install, the upload preview and the registry share:
/// inclusive bounds, a missing bound is no limit, semantic version order with a pre-release below its release, a host
/// that is not a version is not refused and a bound that is not a version fails closed. No container or database.
/// </summary>
public sealed class PluginHostCompatibilityTests
{
    /// <summary>Each host and range answers as the semantic version order says.</summary>
    /// <param name="host">The running host version.</param>
    /// <param name="min">The lower bound, or <see langword="null"/>.</param>
    /// <param name="max">The upper bound, or <see langword="null"/>.</param>
    /// <param name="expected">Whether the host is inside the range.</param>
    [Theory]
    [InlineData("2.0.0", "1.0.0", "3.0.0", true)]
    [InlineData("2.0.0", "2.0.0", "2.0.0", true)]
    [InlineData("2.0.0", "2.0.1", null, false)]
    [InlineData("2.0.0", null, "1.9.9", false)]
    [InlineData("2.0.0", null, null, true)]
    [InlineData("2.0.0", "", " ", true)]
    [InlineData("2.0.0", null, "10.0.0", true)]
    [InlineData("2.0.0-rc.1", "2.0.0", null, false)]
    [InlineData("2.0.0-rc.1", null, "2.0.0", true)]
    [InlineData("dev", "9.0.0", null, true)]
    [InlineData("2.0.0", "two", null, false)]
    public void IsCompatible_FollowsTheSemanticVersionOrder(string host, string? min, string? max, bool expected)
    {
        PluginHostCompatibility.IsCompatible(host, min, max).Should().Be(expected);
    }
}
