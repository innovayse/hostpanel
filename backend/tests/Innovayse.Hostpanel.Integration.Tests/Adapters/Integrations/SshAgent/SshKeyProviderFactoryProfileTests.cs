using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.SshAgent;

/// <summary>
/// <see cref="SshKeyProviderFactory"/> and the agent profile of a server's module: the profile is the one the plugin that serves
/// the module ships, the profile the host still embeds for a legacy module id applies only while no plugin supplies one, and a
/// module nobody serves has no profile at all.
/// </summary>
public sealed class SshKeyProviderFactoryProfileTests
{
    /// <summary>A module served by a plugin that names a profile is supported with that profile, matched ignoring case.</summary>
    [Fact]
    public void AModuleServedByAPluginWithAProfile_UsesThePluginsProfile()
    {
        var factory = SshAgentTestSeeds.KeyFactory(SshAgentTestSeeds.Plugin("panel-plugin"));

        factory.AgentProfileFor(SshAgentTestSeeds.Module).Should().Be(SshAgentTestSeeds.ProfileId);
        factory.AgentProfileFor(SshAgentTestSeeds.Module.ToUpperInvariant()).Should().Be(SshAgentTestSeeds.ProfileId);
        factory.Supports(SshAgentTestSeeds.Module).Should().BeTrue();
    }

    /// <summary>A module served by a plugin that names no profile has no agent, so it is not supported.</summary>
    [Fact]
    public void AModuleServedByAPluginWithoutAProfile_IsNotSupported()
    {
        var plugin = PanelPluginHarness.Plugin(
            "panel-plugin", typeof(FakePanelPlugin), [SshAgentTestSeeds.Module], [PluginCapabilities.ProvisioningPanel]);
        var factory = SshAgentTestSeeds.KeyFactory(plugin);

        factory.AgentProfileFor(SshAgentTestSeeds.Module).Should().BeNull();
        factory.Supports(SshAgentTestSeeds.Module).Should().BeFalse();
    }

    /// <summary>The plain Linux module is supported with the built-in profile, matched ignoring case, with no plugin installed.</summary>
    [Fact]
    public void TheLinuxModule_UsesTheBuiltInProfile()
    {
        var factory = SshAgentTestSeeds.KeyFactory();

        factory.AgentProfileFor(ServerModules.Linux).Should().Be("generic-linux");
        factory.AgentProfileFor(ServerModules.Linux.ToUpperInvariant()).Should().Be("generic-linux");
        factory.Supports(ServerModules.Linux).Should().BeTrue();
    }

    /// <summary>A module no plugin serves has no profile.</summary>
    [Fact]
    public void AModuleNobodyServes_IsNotSupported()
    {
        var factory = SshAgentTestSeeds.KeyFactory(SshAgentTestSeeds.Plugin("panel-plugin"));

        factory.AgentProfileFor("unserved-module").Should().BeNull();
        factory.AgentProfileFor(ServerModules.CPanel).Should().BeNull();
    }

    /// <summary>Transitional: the legacy module id keeps its embedded profile while no plugin supplies one.</summary>
    [Fact]
    public void TheLegacyModuleWithoutAPlugin_KeepsItsEmbeddedProfile()
    {
        var factory = SshAgentTestSeeds.KeyFactory();

        factory.AgentProfileFor(ServerModules.Cwp7).Should().Be("cwp");
        factory.AgentProfileFor(ServerModules.Cwp7.ToLowerInvariant()).Should().Be("cwp");
    }

    /// <summary>Transitional: a plugin that serves the legacy module and names a profile wins over the embedded one.</summary>
    [Fact]
    public void TheLegacyModuleServedByAPluginWithAProfile_UsesThePluginsProfile()
    {
        var plugin = PanelPluginHarness.Plugin(
            "panel-plugin",
            typeof(FakePanelPlugin),
            [ServerModules.Cwp7],
            [PluginCapabilities.ProvisioningPanel],
            SshAgentTestSeeds.ProfileId,
            SshAgentTestSeeds.Profile("panel-plugin", SshAgentTestSeeds.ProfileId, "[agent]\nprovider = examplepanel\n"));
        var factory = SshAgentTestSeeds.KeyFactory(plugin);

        factory.AgentProfileFor(ServerModules.Cwp7).Should().Be(SshAgentTestSeeds.ProfileId);
    }

    /// <summary>An agent provider is built for a module with a profile and none for a module without one.</summary>
    [Fact]
    public void CreateFor_BuildsAProviderOnlyForAModuleWithAProfile()
    {
        var factory = SshAgentTestSeeds.KeyFactory(SshAgentTestSeeds.Plugin("panel-plugin"));
        var agent = new ServerSshAgentEndpoint(true, true, "h", 22, "h", 22, "hp", "KEY", "SHA256:x", 5, 5);

        factory.CreateFor(new ServerSshTargetDto(7, SshAgentTestSeeds.Module, false, agent)).Should().NotBeNull();
        factory.CreateFor(new ServerSshTargetDto(7, ServerModules.CPanel, false, agent)).Should().BeNull();
        factory.CreateFor(new ServerSshTargetDto(7, ServerModules.Linux, false, agent)).Should().NotBeNull();
    }
}
