namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <c>PluginRegistry.FindAgentProfile</c>: the profile a loaded plugin ships is found by its id, matched exactly, and a plugin
/// without a profile never answers.
/// </summary>
public sealed class PluginRegistryAgentProfileTests
{
    /// <summary>The profile is found by its exact id and carries the plugin that ships it.</summary>
    [Fact]
    public void FindAgentProfile_FindsTheProfileByItsExactId()
    {
        var registry = PluginActivationTestSeeds.Registry(SshAgentTestSeeds.Plugin("panel-plugin"));

        var found = registry.FindAgentProfile(SshAgentTestSeeds.ProfileId);

        found.Should().NotBeNull();
        found!.PluginId.Should().Be("panel-plugin");
        found.Helpers.Keys.Should().Equal(SshAgentTestSeeds.HelperName);
    }

    /// <summary>An id that differs in letter case, or that no plugin ships, finds nothing.</summary>
    [Fact]
    public void FindAgentProfile_DoesNotMatchAnotherIdOrCase()
    {
        var registry = PluginActivationTestSeeds.Registry(SshAgentTestSeeds.Plugin("panel-plugin"));

        registry.FindAgentProfile(SshAgentTestSeeds.ProfileId.ToUpperInvariant()).Should().BeNull();
        registry.FindAgentProfile("other-profile").Should().BeNull();
    }

    /// <summary>A plugin that ships no profile is never found, and an empty registry finds nothing.</summary>
    [Fact]
    public void FindAgentProfile_IgnoresAPluginWithoutAProfile()
    {
        var plain = PanelPluginHarness.Plugin(
            "plain-plugin", typeof(FakePanelPlugin), [SshAgentTestSeeds.Module], ["provisioningPanel"]);

        PluginActivationTestSeeds.Registry(plain).FindAgentProfile(SshAgentTestSeeds.ProfileId).Should().BeNull();
        PluginActivationTestSeeds.Registry().FindAgentProfile(SshAgentTestSeeds.ProfileId).Should().BeNull();
    }
}
