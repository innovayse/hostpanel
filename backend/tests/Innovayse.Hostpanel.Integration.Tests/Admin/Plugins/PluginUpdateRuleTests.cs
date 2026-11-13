using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <see cref="PluginUpdateRule.NewestOffer"/>, the one choice of which version a registry row offers as an update: the
/// newest published version that is newer than the installed and staged ones and whose host range admits the running host.
/// No container or database.
/// </summary>
public sealed class PluginUpdateRuleTests
{
    /// <summary>The newest version that fits is offered, not the registry's newest.</summary>
    [Fact]
    public void NewestOffer_SkipsAVersionTheHostCannotRun()
    {
        var versions = new[] { Version("1.1.0"), Version("1.5.0"), Version("2.0.0", min: "5.0.0") };

        PluginUpdateRule.NewestOffer(versions, "1.4.0", "1.0.0", null)!.Version.Should().Be("1.5.0");
    }

    /// <summary>Versions are ordered as versions, not as text.</summary>
    [Fact]
    public void NewestOffer_OrdersBySemanticVersion()
    {
        var versions = new[] { Version("1.9.0"), Version("1.10.0") };

        PluginUpdateRule.NewestOffer(versions, "2.0.0", "1.0.0", null)!.Version.Should().Be("1.10.0");
    }

    /// <summary>Nothing is offered when no newer version runs on the host.</summary>
    [Fact]
    public void NewestOffer_OffersNothingWhenNoNewerVersionFits()
    {
        var versions = new[] { Version("1.0.0"), Version("2.0.0", min: "5.0.0"), Version("3.0.0", max: "1.0.0") };

        PluginUpdateRule.NewestOffer(versions, "1.4.0", "1.0.0", null).Should().BeNull();
    }

    /// <summary>A version that is not published is never offered, and a version listed twice is judged by its strictest row.</summary>
    [Fact]
    public void NewestOffer_OffersOnlyPublishedVersions()
    {
        var versions = new[]
        {
            Version("1.1.0"),
            Version("1.2.0", PluginRegistryVersionStatus.Yanked),
            Version("1.3.0", PluginRegistryVersionStatus.Deprecated),
            Version("1.4.0", PluginRegistryVersionStatus.InReview),
            Version("1.5.0"),
            Version("1.5.0", PluginRegistryVersionStatus.Yanked),
        };

        PluginUpdateRule.NewestOffer(versions, "2.0.0", "1.0.0", null)!.Version.Should().Be("1.1.0");
    }

    /// <summary>A version already installed or staged is not offered again.</summary>
    [Fact]
    public void NewestOffer_SkipsTheInstalledAndStagedVersions()
    {
        var versions = new[] { Version("1.0.0"), Version("1.5.0"), Version("1.6.0") };

        PluginUpdateRule.NewestOffer(versions, "2.0.0", "1.0.0", "1.6.0").Should().BeNull();
    }

    /// <summary>A host that is not a version (a local build) is not refused.</summary>
    [Fact]
    public void NewestOffer_DoesNotRefuseALocalBuild()
    {
        var versions = new[] { Version("2.0.0", min: "5.0.0") };

        PluginUpdateRule.NewestOffer(versions, "dev", "1.0.0", null)!.Version.Should().Be("2.0.0");
    }

    /// <summary>Builds one listed registry version.</summary>
    /// <param name="version">The version text.</param>
    /// <param name="status">Where the version stands in the registry.</param>
    /// <param name="min">The oldest host version it runs on, or <see langword="null"/>.</param>
    /// <param name="max">The newest host version it runs on, or <see langword="null"/>.</param>
    /// <returns>The version row.</returns>
    private static RegistryPluginVersion Version(
        string version, PluginRegistryVersionStatus status = PluginRegistryVersionStatus.Published, string? min = null, string? max = null)
    {
        return new RegistryPluginVersion(version, "1.0", 1, DateTimeOffset.UtcNow, status, null, null, min, max, null, [], null, []);
    }
}
