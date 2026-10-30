using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.SDK.Plugins;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Fulfilment;
using Innovayse.SDK.Plugins.Hosting;
using Innovayse.SDK.Plugins.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginLoader"/> registers each plugin under the SDK contract of the type its manifest declares,
/// the fulfilment, identity and hosted types included. No container or database: the registration is checked
/// on a bare service collection.
/// </summary>
public sealed class PluginLoaderTests
{
    /// <summary>A plugin is registered as a keyed scoped service of its type's contract, keyed by its id.</summary>
    /// <param name="type">The plugin type the manifest declares.</param>
    /// <param name="contract">The SDK contract that type is registered under.</param>
    /// <param name="implementation">The plugin's entry point type.</param>
    [Theory]
    [InlineData(PluginType.Payment, typeof(IPaymentPlugin), typeof(ConfigurationCapturingPaymentPlugin))]
    [InlineData(PluginType.Fulfilment, typeof(IFulfilmentPlugin), typeof(ConfigurationCapturingFulfilmentPlugin))]
    [InlineData(PluginType.Identity, typeof(IClientResolver), typeof(ConfigurationCapturingClientResolver))]
    [InlineData(PluginType.Hosted, typeof(IHostedPlugin), typeof(RecordingHostedPlugin))]
    public void RegisterPluginType_RegistersTheContractOfTheDeclaredType(PluginType type, Type contract, Type implementation)
    {
        var services = new ServiceCollection();
        var plugin = PluginActivationTestSeeds.Plugin(PluginActivationTestSeeds.NewId("loaded"), type, implementation);

        PluginLoader.RegisterPluginType(services, plugin.Manifest, implementation, NullLogger.Instance);

        var descriptor = services.Should().ContainSingle().Subject;
        descriptor.IsKeyedService.Should().BeTrue();
        descriptor.ServiceKey.Should().Be(plugin.Manifest.Id);
        descriptor.ServiceType.Should().Be(contract);
        descriptor.KeyedImplementationType.Should().Be(implementation);
        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }
}
