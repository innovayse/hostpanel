using Innovayse.Hostpanel.Domain.Plugins.Enums;
using Innovayse.Hostpanel.Domain.Settings;

namespace Innovayse.Hostpanel.Domain.Tests.Settings;

/// <summary>The registry trust setting: seeded strict, accepts only the policy names, reads damage as strict, and is audited.</summary>
public sealed class PluginRegistryTrustSettingTests
{
    /// <summary>The setting is seeded <c>VerifiedOnly</c> and the seeded value passes its own pattern.</summary>
    [Fact]
    public void Defaults_SeedVerifiedOnly()
    {
        var seeded = PluginSettingKeys.Defaults.Single(d => d.Key == PluginSettingKeys.RegistryTrust);

        seeded.Value.Should().Be("VerifiedOnly");
        PluginSettingKeys.Patterns[seeded.Key].Regex.IsMatch(seeded.Value).Should().BeTrue();
        PluginSettingKeys.RegistryTrust.Should().Be("plugins.registry_trust");
    }

    /// <summary>Every policy name is accepted in any case and stored in its canonical spelling; nothing else is accepted.</summary>
    [Fact]
    public void Pattern_AcceptsOnlyThePolicyNames()
    {
        var pattern = PluginSettingKeys.Patterns[PluginSettingKeys.RegistryTrust];

        foreach (var name in Enum.GetNames<PluginRegistryTrust>())
        {
            pattern.Regex.IsMatch(name).Should().BeTrue();
            pattern.Regex.IsMatch(name.ToUpperInvariant()).Should().BeTrue();
            pattern.Canonical(name.ToLowerInvariant()).Should().Be(name);
        }

        foreach (var bad in new[] { "", "Everything", "VerifiedOnly,OwnKeysOnly", "1", " VerifiedOnly" })
        {
            pattern.Regex.IsMatch(bad).Should().BeFalse();
        }
    }

    /// <summary>A missing, empty, numeric or unknown stored value reads as the strictest policy; a known name reads as itself.</summary>
    /// <param name="stored">The stored value.</param>
    /// <param name="expected">The policy it reads as.</param>
    [Theory]
    [InlineData(null, PluginRegistryTrust.VerifiedOnly)]
    [InlineData("", PluginRegistryTrust.VerifiedOnly)]
    [InlineData("99", PluginRegistryTrust.VerifiedOnly)]
    [InlineData("0", PluginRegistryTrust.VerifiedOnly)]
    [InlineData("anything", PluginRegistryTrust.VerifiedOnly)]
    [InlineData("AnyRegistryPublisher", PluginRegistryTrust.AnyRegistryPublisher)]
    [InlineData("ownkeysonly", PluginRegistryTrust.OwnKeysOnly)]
    public void ParseRegistryTrust_FallsBackToTheStrictestPolicy(string? stored, PluginRegistryTrust expected)
    {
        PluginSettingKeys.ParseRegistryTrust(stored).Should().Be(expected);
    }

    /// <summary>A change of the policy is written to the activity log, because it widens or narrows what may be installed.</summary>
    [Fact]
    public void ChangesAreAudited()
    {
        PluginSettingKeys.IsAudited(PluginSettingKeys.RegistryTrust).Should().BeTrue();
    }
}
