using Innovayse.Hostpanel.Domain.Settings;

namespace Innovayse.Hostpanel.Domain.Tests.Settings;

/// <summary>The plugin system's setting vocabulary.</summary>
public sealed class PluginSettingKeysTests
{
    /// <summary>The unsigned switch is seeded off, and its seeded value passes its own pattern.</summary>
    [Fact]
    public void Defaults_SeedAllowUnsignedOffAndMatchTheirPatterns()
    {
        var seeded = PluginSettingKeys.Defaults.Single(d => d.Key == PluginSettingKeys.AllowUnsigned);

        seeded.Value.Should().Be(PluginSettingKeys.Off);
        PluginSettingKeys.Patterns[seeded.Key].Regex.IsMatch(seeded.Value).Should().BeTrue();
    }

    /// <summary>The hidden widgets list is seeded empty, so it hides nothing, and an empty value is allowed for it alone.</summary>
    [Fact]
    public void Defaults_SeedHiddenSlotWidgetsEmpty()
    {
        var seeded = PluginSettingKeys.Defaults.Single(d => d.Key == PluginSettingKeys.HiddenSlotWidgets);

        seeded.Value.Should().BeEmpty();
        PluginSettingKeys.AllowsEmpty(PluginSettingKeys.HiddenSlotWidgets).Should().BeTrue();
        PluginSettingKeys.AllowsEmpty(PluginSettingKeys.AllowUnsigned).Should().BeFalse();
    }

    /// <summary>The hidden widgets list accepts <c>slot:plugin:widget</c> entries separated by commas and nothing else.</summary>
    /// <param name="value">The candidate value.</param>
    /// <param name="ok">Whether the pattern accepts it.</param>
    [Theory]
    [InlineData("client.dashboard.cards:acme:usage", true)]
    [InlineData("client.dashboard.cards:acme:usage, admin.client.panels:acme:notes", true)]
    [InlineData("client.dashboard.cards:acme", false)]
    [InlineData("client.dashboard.cards:acme:usage:extra", false)]
    [InlineData("client.dashboard.cards:acme:usage,", true)]
    [InlineData("a:b:c,,", true)]
    [InlineData(",,a:b:c,,d:e:f,", true)]
    [InlineData("a:b:c,d:e", false)]
    [InlineData("a:b:c,,d:e:", false)]
    [InlineData(",", true)]
    [InlineData("a b:acme:usage", false)]
    public void HiddenSlotWidgetsPattern_AcceptsOnlyEntries(string value, bool ok)
    {
        PluginSettingKeys.Patterns[PluginSettingKeys.HiddenSlotWidgets].Regex.IsMatch(value).Should().Be(ok);
    }

    /// <summary>The canonical spelling drops the spaces around entries, and an entry reads back as the one that was written.</summary>
    [Fact]
    public void HiddenWidgets_ReadBackTheEntriesWritten()
    {
        var pattern = PluginSettingKeys.Patterns[PluginSettingKeys.HiddenSlotWidgets];
        var first = PluginSettingKeys.HiddenWidgetEntry("client.dashboard.cards", "acme", "usage");
        var second = PluginSettingKeys.HiddenWidgetEntry("admin.client.panels", "acme", "notes");

        var stored = pattern.Canonical($" {first} ,  {second} ");
        var hidden = PluginSettingKeys.HiddenWidgets(stored);

        stored.Should().Be($"{first},{second}");
        hidden.Should().BeEquivalentTo([first, second]);
        PluginSettingKeys.HiddenWidgets(null).Should().BeEmpty();
        PluginSettingKeys.HiddenWidgets(string.Empty).Should().BeEmpty();
    }

    /// <summary>
    /// The validator and the reader are one parse: every value the pattern accepts reads back as exactly its well formed
    /// entries, empty entries included, and a value the pattern refuses is never stored, so nothing malformed is read.
    /// </summary>
    /// <param name="value">The candidate value.</param>
    /// <param name="expected">The entries the reader returns for it.</param>
    [Theory]
    [InlineData("a:b:c,,", new[] { "a:b:c" })]
    [InlineData(",,a:b:c,,d:e:f,", new[] { "a:b:c", "d:e:f" })]
    [InlineData(" a:b:c , d:e:f ", new[] { "a:b:c", "d:e:f" })]
    [InlineData(",", new string[0])]
    [InlineData("", new string[0])]
    public void HiddenWidgets_AcceptedValueReadsBackItsEntriesAndCanonicalMatches(string value, string[] expected)
    {
        var pattern = PluginSettingKeys.Patterns[PluginSettingKeys.HiddenSlotWidgets];

        pattern.Regex.IsMatch(value).Should().BeTrue();
        PluginSettingKeys.HiddenWidgets(value).Should().BeEquivalentTo(expected);
        PluginSettingKeys.HiddenWidgets(pattern.Canonical(value)).Should().BeEquivalentTo(expected);
        pattern.Regex.IsMatch(pattern.Canonical(value)).Should().BeTrue();
    }

    /// <summary>A malformed entry fails the pattern and is not read as hidden either, so the two sides never disagree.</summary>
    /// <param name="value">The malformed value.</param>
    [Theory]
    [InlineData("a:b")]
    [InlineData("a:b:c:d")]
    [InlineData("a:b:c,d:e")]
    [InlineData("a:b:c,,x y:b:c")]
    public void HiddenWidgets_MalformedEntryFailsThePatternAndIsNotRead(string value)
    {
        PluginSettingKeys.Patterns[PluginSettingKeys.HiddenSlotWidgets].Regex.IsMatch(value).Should().BeFalse();
        PluginSettingKeys.HiddenWidgets(value).Should().NotContain(entry => entry.Split(':', StringSplitOptions.None).Length != 3 || entry.Contains(' '));
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
