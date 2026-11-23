using System.Text;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// The host's agent adapter over the plugin packaging package's agent checker: a valid profile with its helpers is read into
/// the host's profile DTO, a manifest naming none reads as nothing, and each kind of refusal maps to the host's manifest rule.
/// The checks themselves (keys, paths, sizes, links) are the package's own tests. Temp folders only; no container or database.
/// </summary>
public sealed class PluginAgentReaderTests : IDisposable
{
    /// <summary>The profile id the manifests of these tests name.</summary>
    private const string ProfileId = "examplepanel";

    /// <summary>A profile that meets every rule of the package check.</summary>
    private const string GoodProfile = "[agent]\nprovider = examplepanel\nbinding =\nmax_keys = 20\n";

    /// <summary>A scratch plugin folder per test instance, removed on disposal.</summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"agent-rules-{Guid.NewGuid():N}");

    /// <summary>Creates the scratch folder.</summary>
    public PluginAgentReaderTests()
    {
        Directory.CreateDirectory(_folder);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    /// <summary>A manifest that names no agent profile reads as no profile and no violation, whatever the folder holds.</summary>
    [Fact]
    public void NoProfileNamed_IsNeitherAProfileNorAViolation()
    {
        Write("agent/profile.conf", GoodProfile);

        var read = Read(null);

        read.Profile.Should().BeNull();
        read.Violation.Should().BeNull();
    }

    /// <summary>A valid profile and its helpers are read with the plugin's id, the profile id and the exact bytes.</summary>
    [Fact]
    public void ValidProfileWithHelpers_IsReadWithItsBytes()
    {
        Write("agent/profile.conf", GoodProfile);
        Write("agent/helpers/ensure-website", "#!/bin/sh\nexit 0\n");

        var read = Read(ProfileId);

        read.Violation.Should().BeNull();
        read.Profile.Should().NotBeNull();
        read.Profile!.ProfileId.Should().Be(ProfileId);
        read.Profile.PluginId.Should().Be("agent-plugin");
        Encoding.UTF8.GetString(read.Profile.Profile).Should().Be(GoodProfile);
        read.Profile.Helpers.Keys.Should().Equal("ensure-website");
        Encoding.UTF8.GetString(read.Profile.Helpers["ensure-website"]).Should().Be("#!/bin/sh\nexit 0\n");
    }

    /// <summary>A valid profile with no helper is read with an empty helper set.</summary>
    [Fact]
    public void ValidProfileWithoutHelpers_HasNoHelpers()
    {
        Write("agent/profile.conf", GoodProfile);

        var read = Read(ProfileId);

        read.Profile!.Helpers.Should().BeEmpty();
    }

    /// <summary>A manifest naming a profile the package does not carry breaks the missing rule.</summary>
    [Fact]
    public void ProfileFileMissing_BreaksTheMissingRule()
    {
        AssertBreaks(Read(ProfileId), PluginManifestRule.SshAgentProfileMissing);
    }

    /// <summary>A profile that sets a key the agent does not define breaks the file rule.</summary>
    [Fact]
    public void ProfileWithAnUnknownKey_BreaksTheFileRule()
    {
        Write("agent/profile.conf", "[agent]\nprovider = examplepanel\nrun_this = yes\n");

        AssertBreaks(Read(ProfileId), PluginManifestRule.SshAgentProfileFileInvalid);
    }

    /// <summary>A profile whose provider is another id than the manifest names breaks the file rule.</summary>
    [Fact]
    public void ProfileOfAnotherProvider_BreaksTheFileRule()
    {
        Write("agent/profile.conf", "[agent]\nprovider = otherpanel\n");

        AssertBreaks(Read(ProfileId), PluginManifestRule.SshAgentProfileFileInvalid);
    }

    /// <summary>A profile that sets a binding breaks the file rule, because the host sets it per server.</summary>
    [Fact]
    public void ProfileWithABinding_BreaksTheFileRule()
    {
        Write("agent/profile.conf", "[agent]\nprovider = examplepanel\nbinding = 11111111-1111-4111-8111-111111111111\n");

        AssertBreaks(Read(ProfileId), PluginManifestRule.SshAgentProfileFileInvalid);
    }

    /// <summary>A helper with a hidden name breaks the helper rule.</summary>
    [Fact]
    public void HelperWithAHiddenName_BreaksTheHelperRule()
    {
        Write("agent/profile.conf", GoodProfile);
        Write("agent/helpers/.hidden", "#!/bin/sh\n");

        AssertBreaks(Read(ProfileId), PluginManifestRule.SshAgentHelperInvalid);
    }

    /// <summary>Reads the agent files a manifest naming <paramref name="profile"/> declares, from the scratch folder.</summary>
    /// <param name="profile">The manifest <c>sshAgentProfile</c> value, or <see langword="null"/> for a manifest without one.</param>
    /// <returns>The read outcome.</returns>
    private PluginAgentRead Read(string? profile)
    {
        var member = profile is null ? string.Empty : $",\"sshAgentProfile\":\"{profile}\"";
        var manifest = PluginManifestReader.Deserialize(PluginsTestSeeds.ManifestJson("agent-plugin", member))!;
        return PluginAgentReader.Read(_folder, manifest);
    }

    /// <summary>Writes a text file under the scratch folder.</summary>
    /// <param name="path">The package-relative path.</param>
    /// <param name="content">The file text.</param>
    private void Write(string path, string content)
    {
        var full = Path.Combine(_folder, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    /// <summary>Asserts that reading breaks one given rule and yields no profile.</summary>
    /// <param name="read">The outcome.</param>
    /// <param name="rule">The rule it must break.</param>
    private static void AssertBreaks(PluginAgentRead read, PluginManifestRule rule)
    {
        read.Violation.Should().NotBeNull();
        read.Violation!.Rule.Should().Be(rule);
        read.Profile.Should().BeNull();
    }
}
