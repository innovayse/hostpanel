using Innovayse.Hostpanel.Domain.Settings;

namespace Innovayse.Hostpanel.Domain.Tests.Settings;

/// <summary>The plugin system's setting vocabulary.</summary>
public sealed class PluginSettingKeysTests
{
    /// <summary>The setting is seeded off, and its seeded value passes its own pattern.</summary>
    [Fact]
    public void Defaults_SeedAllowUnsignedOffAndMatchTheirPatterns()
    {
        var seeded = PluginSettingKeys.Defaults.Should().ContainSingle().Subject;

        seeded.Key.Should().Be(PluginSettingKeys.AllowUnsigned);
        seeded.Value.Should().Be(PluginSettingKeys.Off);
        PluginSettingKeys.Patterns[seeded.Key].Regex.IsMatch(seeded.Value).Should().BeTrue();
    }

    /// <summary>Only <c>true</c> and <c>false</c> are accepted, in any case, and stored in lower case.</summary>
    /// <param name="value">The candidate value.</param>
    /// <param name="ok">Whether the pattern accepts it.</param>
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", true)]
    [InlineData("TRUE", true)]
    [InlineData("False", true)]
    [InlineData("", false)]
    [InlineData("yes", false)]
    [InlineData("1", false)]
    [InlineData("true ", false)]
    public void Pattern_AcceptsOnlyABoolean(string value, bool ok)
    {
        var pattern = PluginSettingKeys.Patterns[PluginSettingKeys.AllowUnsigned];

        pattern.Regex.IsMatch(value).Should().Be(ok);
        if (ok)
        {
            pattern.Canonical(value).Should().Be(value.ToLowerInvariant());
        }
    }

    /// <summary>A stored value reads as on only when it is <c>true</c>; a missing row or an odd value is off, the safe answer.</summary>
    /// <param name="value">The stored value, or null for a missing row.</param>
    /// <param name="on">Whether it reads as on.</param>
    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("", false)]
    [InlineData("garbage", false)]
    [InlineData(null, false)]
    public void IsOn_ReadsOnlyTrueAsOn(string? value, bool on)
    {
        PluginSettingKeys.IsOn(value).Should().Be(on);
    }

    /// <summary>The setting that weakens a security control is audited, and the key is not public.</summary>
    [Fact]
    public void AllowUnsigned_IsAuditedAndNotPublic()
    {
        PluginSettingKeys.IsAudited(PluginSettingKeys.AllowUnsigned).Should().BeTrue();
        PluginSettingKeys.IsAudited("portal.site.name").Should().BeFalse();
        PluginSettingKeys.Defaults.Select(d => d.Key).Should().NotIntersectWith(PortalSettingKeys.Public);
    }
}
