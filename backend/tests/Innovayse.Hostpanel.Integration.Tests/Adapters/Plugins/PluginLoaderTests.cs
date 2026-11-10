using Hostpanel.Sdk.Plugins.Endpoints;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Fulfilment;
using Hostpanel.Sdk.Plugins.Hosting;
using Hostpanel.Sdk.Plugins.Identity;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Payment;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Innovayse.Hostpanel.Integration.Tests.Fixtures.Enums;
using Innovayse.Hostpanel.Integration.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PluginSignatureRule = Hostpanel.Sdk.Packaging.Enums.PluginSignatureRule;

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

        // The loader also registers the plugin's named HTTP client; only the keyed plugin registrations are under test.
        var descriptor = services.Where(d => d.IsKeyedService && Equals(d.ServiceKey, plugin.Manifest.Id)).Should().ContainSingle().Subject;
        descriptor.IsKeyedService.Should().BeTrue();
        descriptor.ServiceKey.Should().Be(plugin.Manifest.Id);
        descriptor.ServiceType.Should().Be(contract);
        descriptor.KeyedImplementationType.Should().Be(implementation);
        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    /// <summary>
    /// A plugin of a non-app type that lists the <c>endpoints</c> capability is registered under its own contract and also
    /// under the endpoints contract; without the capability only under its own.
    /// </summary>
    /// <param name="declaresEndpoints">Whether the manifest lists the capability.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RegisterPluginType_RegistersEndpointsForANonAppPluginOnlyWhenItDeclaresTheCapability(bool declaresEndpoints)
    {
        var services = new ServiceCollection();
        var id = PluginActivationTestSeeds.NewId("pay");
        var manifest = new PluginManifest
        {
            Id = id,
            Name = "Test plugin",
            Version = "1.0.0",
            Author = "Tests",
            Description = "Plugin used by the plugin runtime tests.",
            Type = PluginType.Payment,
            Category = "Payment",
            EntryPoint = typeof(ConfigurationCapturingPaymentPlugin).FullName!,
            SdkVersion = "1.0.0",
            Capabilities = declaresEndpoints ? [PluginCapabilities.Endpoints] : [],
        };

        PluginLoader.RegisterPluginType(services, manifest, typeof(ConfigurationCapturingPaymentPlugin), NullLogger.Instance);

        var keyed = services.Where(d => d.IsKeyedService && Equals(d.ServiceKey, id)).Select(d => d.ServiceType).ToList();
        keyed.Should().Contain(typeof(IPaymentPlugin));
        if (declaresEndpoints)
        {
            keyed.Should().Contain(typeof(IPluginEndpoints));
        }
        else
        {
            keyed.Should().NotContain(typeof(IPluginEndpoints));
        }
    }

    /// <summary>
    /// A plugin that lists the <c>endpoints</c> capability but whose entry type does not implement the endpoints contract
    /// is refused at load with the endpoints rule, kept in the registry as refused and never registered, while the plugin
    /// beside it still loads.
    /// </summary>
    [Fact]
    public void DiscoverAndRegister_EndpointsDeclaredButNotImplemented_IsRefusedAtLoad()
    {
        var root = Path.Combine(Path.GetTempPath(), $"loader-endpoints-{Guid.NewGuid():N}");
        try
        {
            WriteAppPlugin(root, "good-plugin");
            var folder = Directory.CreateDirectory(Path.Combine(root, "liar-plugin")).FullName;
            File.WriteAllText(
                Path.Combine(folder, "plugin.json"),
                PluginsTestSeeds.ManifestJson("liar-plugin", ",\"capabilities\":[\"endpoints\"]")
                    .Replace("\"Provisioning\"", "\"Payment\"", StringComparison.Ordinal)
                    .Replace("Test.Plugin", "Fixture.PlainPlugin", StringComparison.Ordinal));
            var assemblyName = "Fixture.Plain." + Guid.NewGuid().ToString("N");
            File.WriteAllBytes(Path.Combine(folder, assemblyName + ".dll"), PluginAssemblyFixtures.PlainPlugin(assemblyName, "Fixture.PlainPlugin"));
            var services = new ServiceCollection();

            PluginLoader.DiscoverAndRegister(
                services,
                [new PluginRoot(root, PluginOrigin.Uploaded)],
                PluginSigningTestSeeds.Verifier(enforced: false),
                allowUnsigned: false,
                PluginOptions.DefaultMaxViewBytes,
                NullLoggerFactory.Instance);

            var registry = services.BuildServiceProvider().GetRequiredService<PluginRegistry>();
            registry.IsLoaded("good-plugin").Should().BeTrue();
            registry.IsLoaded("liar-plugin").Should().BeFalse();
            registry.GetRefusedPlugins().Should().ContainSingle(r =>
                r.PluginId == "liar-plugin" && r.Rule == PluginManifestRule.EndpointsNotImplemented);
            services.Should().NotContain(d => d.IsKeyedService && Equals(d.ServiceKey, "liar-plugin"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>The endpoints rule maps to its own error code.</summary>
    [Fact]
    public void CodeFor_EndpointsNotImplemented_IsPluginEndpointsNotImplemented()
    {
        PluginPackageErrors.CodeFor(PluginManifestRule.EndpointsNotImplemented)
            .Should().Be(HostpanelErrorCodes.PLUGIN_ENDPOINTS_NOT_IMPLEMENTED);
    }

    /// <summary>
    /// A non-app plugin declaring the endpoints capability is registered under its type's contract and under
    /// <see cref="IPluginEndpoints"/>, keyed by its id, so the route door and its fulfilment calls reach one class (#818).
    /// </summary>
    [Fact]
    public void RegisterPluginType_FulfilmentWithEndpointsCapability_AlsoRegistersEndpoints()
    {
        var services = new ServiceCollection();
        var plugin = PluginCapabilitySeeds.Plugin(
            PluginActivationTestSeeds.NewId("sell"), PluginType.Fulfilment, typeof(FulfilmentEndpointsPlugin), PluginCapabilities.Endpoints);

        PluginLoader.RegisterPluginType(services, plugin.Manifest, typeof(FulfilmentEndpointsPlugin), NullLogger.Instance);

        // The plugin's named HTTP client adds keyed resilience pipelines; only the plugin id keys are under test.
        var keyed = services.Where(d => d.IsKeyedService && Equals(d.ServiceKey, plugin.Manifest.Id)).ToList();
        keyed.Select(d => d.ServiceType).Should().BeEquivalentTo([typeof(IFulfilmentPlugin), typeof(IPluginEndpoints)]);
        keyed.Should().OnlyContain(d => Equals(d.ServiceKey, plugin.Manifest.Id)
            && d.KeyedImplementationType == typeof(FulfilmentEndpointsPlugin)
            && d.Lifetime == ServiceLifetime.Scoped);
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
                    PluginsTestSeeds.ManifestJson(id, extra, "App")
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
                PluginOptions.DefaultMaxViewBytes,
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
    /// A plugin that is already installed and whose view repeats a block id, or carries a mistyped display member
    /// (rules added after it was installed), still loads: the loader writes one warning per fault and does not skip it.
    /// A view that breaks a rule enforced from the start still skips the plugin. The same repeated id is refused at
    /// install (<c>InstallPluginTests</c>) and by <see cref="PluginViewRead.RefusedAtInstall"/>.
    /// </summary>
    [Fact]
    public void DiscoverAndRegister_InstalledPluginWithRuleAddedLater_LoadsWithAWarning()
    {
        var root = PluginSigningTestSeeds.NewFolder("loader-advisory");
        try
        {
            const string repeated = """
                {"schema":1,"blocks":[{"kind":"text","id":"a","text":"x"},{"kind":"text","id":"a","text":"y"},
                {"kind":"table","display":"grid","columns":[]}]}
                """;
            const string brokenAlways = """{"schema":1,"blocks":[{"kind":"nope"}]}""";
            WriteViewPlugin(root, "repeats-an-id", repeated);
            WriteViewPlugin(root, "breaks-an-old-rule", brokenAlways);

            var (registry, logs) = Discover(
                [new PluginRoot(root, PluginOrigin.Uploaded)],
                PluginSigningTestSeeds.Verifier(enforced: false),
                allowUnsigned: false);

            registry.IsLoaded("repeats-an-id").Should().BeTrue("an installed plugin is not lost to a stricter host");
            registry.IsLoaded("breaks-an-old-rule").Should().BeFalse();
            logs.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning
                && e.Message.Contains("repeats-an-id")
                && e.Message.Contains(nameof(PluginManifestRule.ViewBlockIdRepeated))
                && e.Message.Contains("stays loaded"));
            logs.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning
                && e.Message.Contains("repeats-an-id")
                && e.Message.Contains(nameof(PluginManifestRule.ViewInvalid))
                && e.Message.Contains("stays loaded"));
            logs.Entries.Should().ContainSingle(e => e.Message.Contains("breaks-an-old-rule")
                && e.Message.Contains(nameof(PluginManifestRule.ViewBlockKindUnknown))
                && e.Message.Contains("Skipping"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// A plugin whose view only breaks a rule added later, but which is skipped by a later check (here it has no
    /// assembly), never gets the "stays loaded" line: the advisory is logged only once the plugin has loaded.
    /// </summary>
    [Fact]
    public void DiscoverAndRegister_AdvisoryPluginSkippedLater_DoesNotSayItStaysLoaded()
    {
        var root = PluginSigningTestSeeds.NewFolder("loader-advisory-skipped");
        try
        {
            const string repeated = """
                {"schema":1,"blocks":[{"kind":"text","id":"a","text":"x"},{"kind":"text","id":"a","text":"y"}]}
                """;
            WriteViewPlugin(root, "no-assembly", repeated);
            foreach (var dll in Directory.GetFiles(Path.Combine(root, "no-assembly"), "*.dll"))
            {
                File.Delete(dll);
            }

            var (registry, logs) = Discover(
                [new PluginRoot(root, PluginOrigin.Uploaded)],
                PluginSigningTestSeeds.Verifier(enforced: false),
                allowUnsigned: false);

            registry.IsLoaded("no-assembly").Should().BeFalse();
            logs.Entries.Should().NotContain(e => e.Message.Contains("stays loaded"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// A Production host skips an uploaded plugin that is unsigned, tampered with, signed by an unknown key or holds a link: the
    /// warning names the plugin id and the rule, the plugin's assembly is never loaded, and the signed plugin beside
    /// it still loads.
    /// </summary>
    /// <param name="flaw">What is wrong with the refused plugin.</param>
    /// <param name="rule">The rule the warning names.</param>
    [Theory]
    [InlineData(PluginPackageFlaw.Unsigned, PluginSignatureRule.Unsigned)]
    [InlineData(PluginPackageFlaw.Tampered, PluginSignatureRule.Invalid)]
    [InlineData(PluginPackageFlaw.UnknownKey, PluginSignatureRule.UnknownKey)]
    [InlineData(PluginPackageFlaw.Linked, PluginSignatureRule.Malformed)]
    public void DiscoverAndRegister_Production_SkipsOnlyTheUploadedPluginThatFailsTheSignature(
        PluginPackageFlaw flaw, PluginSignatureRule rule)
    {
        var root = PluginSigningTestSeeds.NewFolder("loader-signed");
        var linkTarget = root + "-linktarget";
        try
        {
            var good = WriteAppPlugin(root, "signed-plugin");
            PluginSigningTestSeeds.Sign(good.Folder);
            var bad = WriteAppPlugin(root, "flawed-plugin");
            Flaw(bad.Folder, flaw, linkTarget);

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
            if (Directory.Exists(linkTarget))
            {
                Directory.Delete(linkTarget, recursive: true);
            }
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

    /// <summary>
    /// A plugin folder named with a reserved id is skipped with a warning naming the id, and a plain folder whose
    /// manifest declares a reserved id is refused by the manifest rule <see cref="PluginManifestRule.PluginIdInvalid"/>;
    /// the plugin beside them still loads.
    /// </summary>
    [Fact]
    public void DiscoverAndRegister_ReservedId_SkipsThePluginWithAWarning()
    {
        var root = PluginSigningTestSeeds.NewFolder("loader-reserved");
        try
        {
            WriteAppPlugin(root, "plain-plugin");
            WriteAppPlugin(root, "ui");
            var declaring = WriteAppPlugin(root, "declares-reserved");
            var manifestPath = Path.Combine(declaring.Folder, "plugin.json");
            File.WriteAllText(
                manifestPath,
                File.ReadAllText(manifestPath).Replace("\"declares-reserved\"", "\"ui\"", StringComparison.Ordinal));

            var (registry, logs) = Discover(
                [new PluginRoot(root, PluginOrigin.Uploaded)], PluginSigningTestSeeds.Verifier(enforced: false), allowUnsigned: false);

            registry.IsLoaded("plain-plugin").Should().BeTrue();
            registry.IsLoaded("ui").Should().BeFalse();
            logs.Entries.Should().Contain(e => e.Level == LogLevel.Warning
                && e.Message.Contains("reserved plugin id") && e.Message.Contains("ui"));
            logs.Entries.Should().Contain(e => e.Level == LogLevel.Warning
                && e.Message.Contains(nameof(PluginManifestRule.PluginIdInvalid)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// A plugin whose entry point type derives from an assembly the host cannot find (the case of a plugin built against an
    /// SDK version the host does not carry) is skipped, and the log says why: the assembly it needs could not be loaded.
    /// The load used to fail as a bare "entry point type not found", which hid the cause.
    /// </summary>
    [Fact]
    public void DiscoverAndRegister_EntryPointDependencyCannotBeLoaded_LogsTheLoadFailure()
    {
        var root = PluginSigningTestSeeds.NewFolder("loader-missing-dependency");
        try
        {
            var folder = Directory.CreateDirectory(Path.Combine(root, "missing-dependency")).FullName;
            File.WriteAllText(
                Path.Combine(folder, "plugin.json"),
                PluginsTestSeeds.ManifestJson("missing-dependency").Replace("\"entryPoint\": \"Test.Plugin\"", "\"entryPoint\": \"Fixture.PluginType\"", StringComparison.Ordinal));
            File.WriteAllBytes(
                Path.Combine(folder, "Fixture.Loaded.dll"),
                PluginAssemblyFixtures.BuiltAgainstAssembly("Fixture.MissingDependency", new Version(3, 1, 0, 0), "Fixture.Loaded"));

            var (registry, logs) = Discover(
                [new PluginRoot(root, PluginOrigin.BuiltIn)], PluginSigningTestSeeds.Verifier(enforced: false), allowUnsigned: false);

            registry.IsLoaded("missing-dependency").Should().BeFalse();
            logs.Entries.Should().Contain(e => e.Level == LogLevel.Error
                && e.Exception != null && e.Exception.Message.Contains("Fixture.MissingDependency"));
            logs.Entries.Should().Contain(e => e.Level == LogLevel.Error && e.Message.Contains("not found for plugin missing-dependency"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
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

        PluginLoader.DiscoverAndRegister(services, roots, verifier, allowUnsigned, PluginOptions.DefaultMaxViewBytes, loggerFactory);

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
            PluginsTestSeeds.ManifestJson(id, type: "App")
                .Replace("Test.Plugin", "Fixture.AppPlugin", StringComparison.Ordinal));

        // A fresh assembly name per plugin: the loader loads into the default context, which holds one assembly per name.
        var assemblyName = "Fixture.Signed." + id + Guid.NewGuid().ToString("N");
        File.WriteAllBytes(Path.Combine(folder, assemblyName + ".dll"), PluginAssemblyFixtures.AppPlugin(assemblyName, "Fixture.AppPlugin"));
        return (folder, assemblyName);
    }

    /// <summary>Writes an app plugin with one client page whose view is the given text.</summary>
    /// <param name="root">The plugin root.</param>
    /// <param name="id">The plugin id, which is also the folder name.</param>
    /// <param name="view">The text of <c>views/a.json</c>.</param>
    private static void WriteViewPlugin(string root, string id, string view)
    {
        var (folder, _) = WriteAppPlugin(root, id);
        File.WriteAllText(
            Path.Combine(folder, "plugin.json"),
            PluginsTestSeeds.ManifestJson(
                    id,
                    ",\"manifestVersion\":2,\"ui\":{\"client\":{\"pages\":"
                    + "[{\"id\":\"overview\",\"path\":\"/o\",\"title\":\"O\","
                    + "\"view\":\"views/a.json\",\"permission\":\"Any\"}]}}",
                    "App")
                .Replace("Test.Plugin", "Fixture.AppPlugin", StringComparison.Ordinal));
        Directory.CreateDirectory(Path.Combine(folder, "views"));
        File.WriteAllText(Path.Combine(folder, "views", "a.json"), view);
    }

    /// <summary>Gives a plugin folder one flaw: nothing, a change after signing, a signature by an unknown key, or a link.</summary>
    /// <param name="folder">The plugin folder.</param>
    /// <param name="flaw">The flaw.</param>
    /// <param name="linkTarget">Where a <see cref="PluginPackageFlaw.Linked"/> folder points; created here, outside the plugin root, and deleted by the caller.</param>
    private static void Flaw(string folder, PluginPackageFlaw flaw, string? linkTarget = null)
    {
        switch (flaw)
        {
            case PluginPackageFlaw.Linked:
                PluginSigningTestSeeds.Sign(folder);
                PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder).Should().BeNull("the link must be the only flaw");
                PluginLinkTestSeeds.CreateDirectoryLink(Path.Combine(folder, "linked"), Directory.CreateDirectory(linkTarget!).FullName);
                break;
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
