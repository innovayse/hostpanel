using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Shared;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// What the loader does with the SSH key agent files of a plugin: the checked profile is kept for the registry, and a profile that
/// breaks a rule or an id another loaded plugin already ships leaves the plugin loaded without a profile and with one warning, so a
/// rule added after the install never takes a running plugin away. Temp folders only; no container or database.
/// </summary>
public sealed class PluginLoaderAgentProfileTests : IDisposable
{
    /// <summary>A profile that meets every rule of the package check.</summary>
    private const string GoodProfile = "[agent]\nprovider = examplepanel\n";

    /// <summary>A scratch plugin folder per test instance, removed on disposal.</summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"loader-agent-{Guid.NewGuid():N}");

    /// <summary>What the loader under test logged.</summary>
    private readonly RecordingLoggerProvider _logs = new();

    /// <summary>Creates the scratch folder.</summary>
    public PluginLoaderAgentProfileTests()
    {
        Directory.CreateDirectory(_folder);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _logs.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    /// <summary>A valid profile is returned for the loader to register, and nothing is logged.</summary>
    [Fact]
    public void ValidProfile_IsReturnedWithoutAWarning()
    {
        Write("agent/profile.conf", GoodProfile);

        var profile = Read(new PluginRegistry());

        profile.Should().NotBeNull();
        profile!.ProfileId.Should().Be("examplepanel");
        profile.PluginId.Should().Be("agent-plugin");
        _logs.Entries.Should().BeEmpty();
    }

    /// <summary>A manifest that names no profile gives none, and the files in the folder are never read.</summary>
    [Fact]
    public void NoProfileNamed_GivesNoProfile()
    {
        Write("agent/profile.conf", GoodProfile);

        var manifest = PluginManifestReader.Deserialize(PluginsTestSeeds.ManifestJson("agent-plugin"))!;

        PluginLoader.ReadAgentProfile(new PluginRegistry(), manifest, _folder, Logger()).Should().BeNull();
        _logs.Entries.Should().BeEmpty();
    }

    /// <summary>A profile that breaks a rule gives none and one warning that names the rule, so the plugin stays loaded.</summary>
    [Fact]
    public void ProfileBreakingARule_GivesNoProfileAndOneWarning()
    {
        Write("agent/profile.conf", "[agent]\nprovider = examplepanel\nrun_this = yes\n");

        var profile = Read(new PluginRegistry());

        profile.Should().BeNull();
        _logs.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning
            && e.Message.Contains("agent-plugin")
            && e.Message.Contains(nameof(PluginManifestRule.SshAgentProfileFileInvalid))
            && e.Message.Contains("stays loaded"));
    }

    /// <summary>A profile id another loaded plugin already ships gives none and one warning that names the other plugin.</summary>
    [Fact]
    public void ProfileIdAlreadyShippedByAnotherPlugin_GivesNoProfileAndOneWarning()
    {
        Write("agent/profile.conf", GoodProfile);
        var registry = PluginActivationTestSeeds.Registry(SshAgentTestSeeds.Plugin("first-plugin"));

        var profile = Read(registry);

        profile.Should().BeNull();
        registry.FindAgentProfile("examplepanel")!.PluginId.Should().Be("first-plugin");
        _logs.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning
            && e.Message.Contains("agent-plugin")
            && e.Message.Contains("first-plugin")
            && e.Message.Contains("stays loaded"));
    }

    /// <summary>Runs the loader's agent step for a manifest that names the profile, over the scratch folder.</summary>
    /// <param name="registry">The registry holding the plugins loaded so far.</param>
    /// <returns>The profile the loader would register.</returns>
    private PluginAgentProfileDto? Read(PluginRegistry registry)
    {
        var manifest = PluginManifestReader.Deserialize(
            PluginsTestSeeds.ManifestJson("agent-plugin", ",\"sshAgentProfile\":\"examplepanel\""))!;
        return PluginLoader.ReadAgentProfile(registry, manifest, _folder, Logger());
    }

    /// <summary>Builds a logger that records into <see cref="_logs"/>.</summary>
    /// <returns>The logger.</returns>
    private ILogger Logger()
    {
        return _logs.CreateLogger("agent-profile-tests");
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
}
