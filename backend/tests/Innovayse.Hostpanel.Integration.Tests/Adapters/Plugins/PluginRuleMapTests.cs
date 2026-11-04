using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Application.Common.Errors;
using PackageArchiveRule = Hostpanel.Sdk.Packaging.Enums.PluginArchiveRule;
using PackageIconRule = Hostpanel.Sdk.Packaging.Enums.PluginIconRule;
using PackageManifestViolation = Hostpanel.Sdk.Packaging.Manifest.PluginManifestViolation;
using PackageManifestRule = Hostpanel.Sdk.Packaging.Enums.PluginManifestRule;
using PackageSignatureRule = Hostpanel.Sdk.Packaging.Enums.PluginSignatureRule;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginRuleMap"/>, the one place the plugin packaging package's rule enums become the host's: every
/// value the package defines maps by name to a host rule with a code (never by a cast of the number), a rule the host
/// has no arm for is refused loudly, and a violation keeps its detail. No container or database.
/// </summary>
public sealed class PluginRuleMapTests
{
    /// <summary>Every manifest rule of the package maps to the host rule of the same name, and that rule has an error code.</summary>
    /// <param name="rule">The package's rule.</param>
    [Theory]
    [MemberData(nameof(ManifestRules))]
    public void EveryManifestRule_MapsByName(PackageManifestRule rule)
    {
        var host = PluginRuleMap.ToHost(rule);

        host.ToString().Should().Be(rule.ToString());
        PluginPackageErrors.CodeFor(host).Should().StartWith("PLUGIN_");
    }

    /// <summary>A manifest rule value the package does not define is refused, not passed through.</summary>
    [Fact]
    public void AnUndefinedManifestRule_IsRefused()
    {
        var map = () => PluginRuleMap.ToHost((PackageManifestRule)9999);

        map.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>The package's icon rules become the host's icon manifest rules, each with an error code.</summary>
    /// <param name="rule">The package's icon rule.</param>
    /// <param name="expected">The host rule.</param>
    [Theory]
    [InlineData(PackageIconRule.Missing, PluginManifestRule.IconMissing)]
    [InlineData(PackageIconRule.Invalid, PluginManifestRule.IconInvalid)]
    public void EachIconRule_MapsToTheHostIconRule(PackageIconRule rule, PluginManifestRule expected)
    {
        PluginRuleMap.ToHost(rule).Should().Be(expected);
        PluginPackageErrors.CodeFor(expected).Should().StartWith("PLUGIN_");
    }

    /// <summary>Every icon rule of the package is covered by the theory above.</summary>
    [Fact]
    public void EveryIconRule_HasAnArm()
    {
        foreach (var rule in Enum.GetValues<PackageIconRule>())
        {
            PluginRuleMap.ToHost(rule).Should().BeOneOf(PluginManifestRule.IconMissing, PluginManifestRule.IconInvalid);
        }
    }

    /// <summary>The package's archive rules become the outcome an install is refused with: too large for the limits, unsafe for a traversal, unwritable for a clash of names.</summary>
    /// <param name="rule">The package's archive rule.</param>
    /// <param name="expected">The refusing outcome.</param>
    [Theory]
    [InlineData(PackageArchiveRule.TooManyEntries, PluginPackageOutcome.ArchiveTooLarge)]
    [InlineData(PackageArchiveRule.TooLarge, PluginPackageOutcome.ArchiveTooLarge)]
    [InlineData(PackageArchiveRule.EntryOutsideFolder, PluginPackageOutcome.ArchiveUnsafe)]
    [InlineData(PackageArchiveRule.EntryNotWritable, PluginPackageOutcome.ArchiveUnwritable)]
    public void EachArchiveRule_MapsToItsOutcome(PackageArchiveRule rule, PluginPackageOutcome expected)
    {
        PluginRuleMap.ToHost(rule).Should().Be(expected);
    }

    /// <summary>Every archive rule of the package has an outcome.</summary>
    [Fact]
    public void EveryArchiveRule_HasAnArm()
    {
        foreach (var rule in Enum.GetValues<PackageArchiveRule>())
        {
            PluginRuleMap.ToHost(rule).Should().BeOneOf(PluginPackageOutcome.ArchiveTooLarge, PluginPackageOutcome.ArchiveUnsafe, PluginPackageOutcome.ArchiveUnwritable);
        }
    }

    /// <summary>An archive entry that cannot be written is refused with its own code, which the resource coverage test pairs with a sentence in every language.</summary>
    [Fact]
    public void AnUnwritableEntry_HasItsOwnRefusalCode()
    {
        var result = new PluginPackageResult(PluginRuleMap.ToHost(PackageArchiveRule.EntryNotWritable), "acme", null);

        PluginPackageErrors.CodeFor(result).Should().Be(HostpanelErrorCodes.PLUGIN_ARCHIVE_UNWRITABLE);
    }

    /// <summary>A violation keeps its detail and takes the host's rule.</summary>
    [Fact]
    public void AViolation_KeepsItsDetailAndTakesTheHostRule()
    {
        var host = PluginRuleMap.ToHost(new PackageManifestViolation(PackageManifestRule.RouteDuplicate, "api.client GET '/a'"));

        host.Rule.Should().Be(PluginManifestRule.RouteDuplicate);
        host.Detail.Should().Be("api.client GET '/a'");
    }

    /// <summary>Every signature rule of the package has an error code, since the host uses the package's enum as it is.</summary>
    [Fact]
    public void EverySignatureRule_HasACode()
    {
        foreach (var rule in Enum.GetValues<PackageSignatureRule>())
        {
            PluginPackageErrors.CodeFor(rule).Should().StartWith("PLUGIN_");
        }
    }

    /// <summary>The package's manifest rules, one per theory case.</summary>
    /// <returns>One row per value of <see cref="PackageManifestRule"/>.</returns>
    public static TheoryData<PackageManifestRule> ManifestRules()
    {
        var data = new TheoryData<PackageManifestRule>();
        foreach (var rule in Enum.GetValues<PackageManifestRule>())
        {
            data.Add(rule);
        }

        return data;
    }
}
