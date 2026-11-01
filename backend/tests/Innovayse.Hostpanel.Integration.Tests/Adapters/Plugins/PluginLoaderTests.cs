using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Innovayse.Hostpanel.Integration.Tests.Fixtures.Enums;
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

            PluginLoader.DiscoverAndRegister(
                services,
                [new PluginRoot(root, PluginOrigin.Uploaded)],
                PluginSigningTestSeeds.Verifier(enforced: false),
                allowUnsigned: false,
                loggerFactory);

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

    /// <summary>
    /// A Production host skips an uploaded plugin that is unsigned, tampered with or signed by an unknown key: the
    /// warning names the plugin id and the rule, the plugin's assembly is never loaded, and the signed plugin beside
    /// it still loads.
    /// </summary>
    /// <param name="flaw">What is wrong with the refused plugin.</param>
    /// <param name="rule">The rule the warning names.</param>
    [Theory]
    [InlineData(PluginPackageFlaw.Unsigned, PluginSignatureRule.Unsigned)]
    [InlineData(PluginPackageFlaw.Tampered, PluginSignatureRule.Invalid)]
    [InlineData(PluginPackageFlaw.UnknownKey, PluginSignatureRule.UnknownKey)]
    public void DiscoverAndRegister_Production_SkipsOnlyTheUploadedPluginThatFailsTheSignature(
        PluginPackageFlaw flaw, PluginSignatureRule rule)
    {
        var root = PluginSigningTestSeeds.NewFolder("loader-signed");
        try
        {
            var good = WriteAppPlugin(root, "signed-plugin");
            PluginSigningTestSeeds.Sign(good.Folder);
            var bad = WriteAppPlugin(root, "flawed-plugin");
            Flaw(bad.Folder, flaw);

            var (registry, logs) = Discover(
                [new PluginRoot(root, PluginOrigin.Uploaded)], PluginSigningTestSeeds.Verifier(enforced: true), allowUnsigned: false);

            registry.IsLoaded("signed-plugin").Should().BeTrue();
            registry.IsLoaded("flawed-plugin").Should().BeFalse();
            logs.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning
                && e.Message.Contains("flawed-plugin")
                && e.Message.Contains(rule.ToString()));
            AppDomain.CurrentDomain.GetAssemblies()
                .Should().NotContain(a => a.GetName().Name == bad.AssemblyName, "a refused plugin's code must not even be loaded");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>With "allow unsigned plugins" on, a Production host loads the plugins it would otherwise skip.</summary>
    /// <param name="flaw">What is wrong with the plugin.</param>
    [Theory]
    [InlineData(PluginPackageFlaw.Unsigned)]
    [InlineData(PluginPackageFlaw.Tampered)]
    [InlineData(PluginPackageFlaw.UnknownKey)]
    public void DiscoverAndRegister_Production_LoadsAFailingPluginWhenUnsignedIsAllowed(PluginPackageFlaw flaw)
    {
        var root = PluginSigningTestSeeds.NewFolder("loader-allowed");
        try
        {
            Flaw(WriteAppPlugin(root, "flawed-plugin").Folder, flaw);

            var (registry, _) = Discover(
                [new PluginRoot(root, PluginOrigin.Uploaded)], PluginSigningTestSeeds.Verifier(enforced: true), allowUnsigned: true);

            registry.IsLoaded("flawed-plugin").Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A Development host loads an unsigned uploaded plugin without the setting.</summary>
    [Fact]
    public void DiscoverAndRegister_Development_LoadsAnUnsignedPlugin()
    {
        var root = PluginSigningTestSeeds.NewFolder("loader-dev");
        try
        {
            WriteAppPlugin(root, "unsigned-plugin");

            var (registry, _) = Discover(
                [new PluginRoot(root, PluginOrigin.Uploaded)], PluginSigningTestSeeds.Verifier(enforced: false), allowUnsigned: false);

            registry.IsLoaded("unsigned-plugin").Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// A plugin shipped inside the host (the built-in root) loads in Production with no signature and the setting off,
    /// while an unsigned plugin of the upload root is skipped in the same run: the rule follows where a plugin comes from.
    /// </summary>
    [Fact]
    public void DiscoverAndRegister_Production_LoadsABuiltInPluginWithoutASignature()
    {
        var builtIn = PluginSigningTestSeeds.NewFolder("loader-builtin");
        var uploaded = PluginSigningTestSeeds.NewFolder("loader-uploaded");
        try
        {
            WriteAppPlugin(builtIn, "shipped-plugin");
            WriteAppPlugin(uploaded, "unsigned-upload");

            var (registry, _) = Discover(
                [new PluginRoot(builtIn, PluginOrigin.BuiltIn), new PluginRoot(uploaded, PluginOrigin.Uploaded)],
                PluginSigningTestSeeds.Verifier(enforced: true),
                allowUnsigned: false);

            registry.IsLoaded("shipped-plugin").Should().BeTrue();
            registry.IsLoaded("unsigned-upload").Should().BeFalse();
        }
        finally
        {
            Directory.Delete(builtIn, recursive: true);
            Directory.Delete(uploaded, recursive: true);
        }
    }

    /// <summary>Runs the loader over the given roots on a bare service collection.</summary>
    /// <param name="roots">The plugin roots, with their origin.</param>
    /// <param name="verifier">The signature policy.</param>
    /// <param name="allowUnsigned">Whether the admin setting is on.</param>
    /// <returns>The registry the loader built and what it logged.</returns>
    private static (IPluginRegistry Registry, RecordingLoggerProvider Logs) Discover(
        IReadOnlyList<PluginRoot> roots, PluginSignatureVerifier verifier, bool allowUnsigned)
    {
        var logs = new RecordingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        var services = new ServiceCollection();

        PluginLoader.DiscoverAndRegister(services, roots, verifier, allowUnsigned, loggerFactory);

        return (services.BuildServiceProvider().GetRequiredService<IPluginRegistry>(), logs);
    }

    /// <summary>Writes an app plugin (manifest and a freshly named assembly) into a new folder under a root.</summary>
    /// <param name="root">The plugin root.</param>
    /// <param name="id">The plugin id, which is also the folder name.</param>
    /// <returns>The plugin's folder and the unique name of its assembly.</returns>
    private static (string Folder, string AssemblyName) WriteAppPlugin(string root, string id)
    {
        var folder = Directory.CreateDirectory(Path.Combine(root, id)).FullName;
        File.WriteAllText(
            Path.Combine(folder, "plugin.json"),
            PluginsTestSeeds.ManifestJson(id)
                .Replace("\"Provisioning\"", "\"App\"", StringComparison.Ordinal)
                .Replace("Test.Plugin", "Fixture.AppPlugin", StringComparison.Ordinal));

        // A fresh assembly name per plugin: the loader loads into the default context, which holds one assembly per name.
        var assemblyName = "Fixture.Signed." + id + Guid.NewGuid().ToString("N");
        File.WriteAllBytes(Path.Combine(folder, assemblyName + ".dll"), PluginAssemblyFixtures.AppPlugin(assemblyName, "Fixture.AppPlugin"));
        return (folder, assemblyName);
    }

    /// <summary>Gives a plugin folder one flaw: nothing, a change after signing, or a signature by an unknown key.</summary>
    /// <param name="folder">The plugin folder.</param>
    /// <param name="flaw">The flaw.</param>
    private static void Flaw(string folder, PluginPackageFlaw flaw)
    {
        switch (flaw)
        {
            case PluginPackageFlaw.Tampered:
                PluginSigningTestSeeds.Sign(folder);
                File.AppendAllText(Path.Combine(folder, "plugin.json"), " ");
                break;
            case PluginPackageFlaw.UnknownKey:
                PluginSigningTestSeeds.Sign(folder, PluginSigningTestSeeds.UntrustedKeyId, PluginSigningTestSeeds.UntrustedKey.PrivateKey);
                break;
            case PluginPackageFlaw.Unsigned:
            default:
                break;
        }
    }
}
