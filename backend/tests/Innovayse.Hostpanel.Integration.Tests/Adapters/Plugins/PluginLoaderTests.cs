using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Innovayse.Hostpanel.Integration.Tests.Shared;
using Innovayse.SDK.Plugins.Endpoints;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Fulfilment;
using Innovayse.SDK.Plugins.Hosting;
using Innovayse.SDK.Plugins.Identity;
using Innovayse.SDK.Plugins.Payment;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginLoader"/> registers each plugin under the SDK contract of the type its manifest declares,
/// the fulfilment, identity, hosted and app types included. No container or database: the registration is checked
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
    [InlineData(PluginType.App, typeof(IPluginEndpoints), typeof(EchoAppPlugin))]
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

    /// <summary>
    /// A plugin whose manifest breaks a rule is skipped with a logged reason naming its id and the rule, and the
    /// plugin beside it still loads: one bad plugin never stops the others.
    /// </summary>
    [Fact]
    public void DiscoverAndRegister_RefusedPlugin_SkipsOnlyThatPlugin()
    {
        var root = Path.Combine(Path.GetTempPath(), $"loader-rules-{Guid.NewGuid():N}");
        try
        {
            foreach (var (id, extra) in new[]
            {
                ("good-plugin", string.Empty),
                ("bad-plugin", ",\"manifestVersion\":2,\"ui\":{\"slots\":[{\"id\":\"s\",\"slot\":\"client.nowhere\",\"view\":\"v.json\"}]}"),
            })
            {
                var folder = Directory.CreateDirectory(Path.Combine(root, id)).FullName;
                File.WriteAllText(
                    Path.Combine(folder, "plugin.json"),
                    PluginsTestSeeds.ManifestJson(id, extra)
                        .Replace("\"Provisioning\"", "\"App\"", StringComparison.Ordinal)
                        .Replace("Test.Plugin", "Fixture.AppPlugin", StringComparison.Ordinal));

                // A fresh assembly per plugin: the loader loads into the default context, which holds one assembly per name.
                File.WriteAllBytes(
                    Path.Combine(folder, "Fixture.Loaded." + id + ".dll"),
                    PluginAssemblyFixtures.AppPlugin("Fixture.Loaded." + id + Guid.NewGuid().ToString("N"), "Fixture.AppPlugin"));
            }

            var logs = new RecordingLoggerProvider();
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
            var services = new ServiceCollection();

            PluginLoader.DiscoverAndRegister(services, [root], loggerFactory);

            var registry = services.BuildServiceProvider().GetRequiredService<IPluginRegistry>();
            registry.IsLoaded("good-plugin").Should().BeTrue();
            registry.IsLoaded("bad-plugin").Should().BeFalse();
            logs.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning
                && e.Message.Contains("bad-plugin")
                && e.Message.Contains(nameof(PluginManifestRule.SlotUnknown)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
