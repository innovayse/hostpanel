using System.Text;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Security;
using Innovayse.Hostpanel.Infrastructure.Security.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>What the SSH key agent profile tests share: a plugin that ships a profile and the real factory and bundles over a registry.</summary>
internal static class SshAgentTestSeeds
{
    /// <summary>The module the plugin of <see cref="Plugin"/> serves.</summary>
    public const string Module = "examplepanel7";

    /// <summary>The profile id the plugin of <see cref="Plugin"/> ships.</summary>
    public const string ProfileId = "examplepanel";

    /// <summary>The name of the one helper the plugin of <see cref="Plugin"/> ships.</summary>
    public const string HelperName = "ensure-website";

    /// <summary>Builds the checked profile a plugin ships, as the loader holds it.</summary>
    /// <param name="pluginId">The plugin that ships it.</param>
    /// <param name="profileId">The profile id.</param>
    /// <param name="profileText">The profile file content.</param>
    /// <returns>The profile with one helper.</returns>
    public static PluginAgentProfileDto Profile(string pluginId, string profileId, string profileText)
    {
        return new PluginAgentProfileDto(
            profileId,
            pluginId,
            Encoding.UTF8.GetBytes(profileText),
            new Dictionary<string, byte[]> { [HelperName] = Encoding.UTF8.GetBytes("#!/bin/sh\nexit 0\n") });
    }

    /// <summary>Builds a panel plugin that serves <see cref="Module"/> and ships a profile.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="profileId">The profile id its manifest names.</param>
    /// <param name="profileText">The profile file content.</param>
    /// <returns>The loaded plugin.</returns>
    public static LoadedPlugin Plugin(string pluginId, string profileId = ProfileId, string profileText = "[agent]\nprovider = examplepanel\n")
    {
        return PanelPluginHarness.Plugin(
            pluginId,
            typeof(FakePanelPlugin),
            [Module],
            [PluginCapabilities.ProvisioningPanel],
            profileId,
            Profile(pluginId, profileId, profileText));
    }

    /// <summary>Builds the real key factory over a registry holding the plugins.</summary>
    /// <param name="plugins">The loaded plugins.</param>
    /// <returns>The factory.</returns>
    public static SshKeyProviderFactory KeyFactory(params LoadedPlugin[] plugins)
    {
        var registry = PluginActivationTestSeeds.Registry(plugins);
        var transport = new SshAgentTransport(
            new OutboundConnector(Options.Create(new OutboundNetworkOptions()), Options.Create(new PanelNetworkOptions())),
            NullLogger<SshAgentTransport>.Instance);
        return new SshKeyProviderFactory(transport, new ServerModulePluginResolver(registry, NullLogger<ServerModulePluginResolver>.Instance), registry);
    }

    /// <summary>Builds the real bundles over a registry holding the plugins.</summary>
    /// <param name="plugins">The loaded plugins.</param>
    /// <returns>The bundles.</returns>
    public static SshAgentBundles Bundles(params LoadedPlugin[] plugins)
    {
        return new SshAgentBundles(PluginActivationTestSeeds.Registry(plugins));
    }
}
