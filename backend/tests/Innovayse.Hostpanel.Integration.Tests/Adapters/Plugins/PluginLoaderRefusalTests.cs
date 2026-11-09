using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Innovayse.Hostpanel.Integration.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// What <see cref="PluginLoader"/> keeps of a plugin it refuses: the id, the broken manifest rule and its detail, for
/// the upload folder and the built-in folder alike, while the refused plugin is never registered as loaded.
/// </summary>
public sealed class PluginLoaderRefusalTests
{
    /// <summary>A plugin that breaks a manifest rule is kept as refused in either folder and is not loaded.</summary>
    [Fact]
    public void RefusedPlugins_AreKeptWithTheirRuleInBothFolders()
    {
        var builtIn = NewRoot();
        var uploaded = NewRoot();
        try
        {
            WriteRefused(builtIn, "refused-built-in");
            WriteRefused(uploaded, "refused-upload");
            var services = new ServiceCollection();

            PluginLoader.DiscoverAndRegister(
                services,
                [new PluginRoot(builtIn, PluginOrigin.BuiltIn), new PluginRoot(uploaded, PluginOrigin.Uploaded)],
                PluginSignatureVerifier.Create(new PluginSigningOptions(), new HostingEnvironment { EnvironmentName = Environments.Development }),
                allowUnsigned: false,
                PluginOptions.DefaultMaxViewBytes,
                NullLoggerFactory.Instance);

            var registry = services.BuildServiceProvider().GetRequiredService<PluginRegistry>();
            registry.LoadedPlugins.Should().BeEmpty("a refused plugin is never loaded");
            var refused = registry.GetRefusedPlugins().ToDictionary(r => r.PluginId);
            refused.Keys.Should().BeEquivalentTo("refused-built-in", "refused-upload");
            refused["refused-built-in"].Rule.Should().Be(PluginManifestRule.ManifestVersionUnknown);
            refused["refused-built-in"].IsBuiltIn.Should().BeTrue();
            refused["refused-upload"].Rule.Should().Be(PluginManifestRule.ManifestVersionUnknown);
            refused["refused-upload"].IsBuiltIn.Should().BeFalse();
            refused["refused-upload"].Detail.Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            Directory.Delete(builtIn, recursive: true);
            Directory.Delete(uploaded, recursive: true);
        }
    }

    /// <summary>
    /// A refused built-in plugin claims its id: a same-named upload, whether it is valid or refused too, is skipped, and
    /// the built-in plugin's refusal entry is not overwritten by the upload's.
    /// </summary>
    [Fact]
    public void RefusedBuiltIn_ClaimsItsIdAgainstASameNamedUpload()
    {
        var builtIn = NewRoot();
        var uploaded = NewRoot();
        try
        {
            WriteRefused(builtIn, "claimed-plugin");
            var folder = Path.Combine(uploaded, "claimed-plugin");
            Directory.CreateDirectory(folder);
            File.WriteAllText(
                Path.Combine(folder, "plugin.json"),
                PluginsTestSeeds.ManifestJson("claimed-plugin").Replace("\"version\": \"1.0.0\"", "\"version\": \"one\""));
            var logs = new RecordingLoggerProvider();
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
            var services = new ServiceCollection();

            PluginLoader.DiscoverAndRegister(
                services,
                [new PluginRoot(builtIn, PluginOrigin.BuiltIn), new PluginRoot(uploaded, PluginOrigin.Uploaded)],
                PluginSignatureVerifier.Create(new PluginSigningOptions(), new HostingEnvironment { EnvironmentName = Environments.Development }),
                allowUnsigned: false,
                PluginOptions.DefaultMaxViewBytes,
                loggerFactory);

            var registry = services.BuildServiceProvider().GetRequiredService<PluginRegistry>();
            var entry = registry.GetRefusedPlugins().Single(r => r.PluginId == "claimed-plugin");
            entry.IsBuiltIn.Should().BeTrue("the built-in refusal is never overwritten by the upload's");
            entry.Rule.Should().Be(PluginManifestRule.ManifestVersionUnknown);
            registry.LoadedPlugins.Should().BeEmpty();
            logs.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Message.Contains("already declared"));
        }
        finally
        {
            Directory.Delete(builtIn, recursive: true);
            Directory.Delete(uploaded, recursive: true);
        }
    }

    /// <summary>A manifest whose version is not a strict version is refused with the version rule and kept for the list.</summary>
    [Fact]
    public void ManifestWithAnInvalidVersion_IsRefusedWithTheVersionRule()
    {
        var uploaded = NewRoot();
        try
        {
            var folder = Path.Combine(uploaded, "bad-version");
            Directory.CreateDirectory(folder);
            File.WriteAllText(
                Path.Combine(folder, "plugin.json"),
                PluginsTestSeeds.ManifestJson("bad-version").Replace("\"version\": \"1.0.0\"", "\"version\": \"01.0\""));
            var services = new ServiceCollection();

            PluginLoader.DiscoverAndRegister(
                services,
                [new PluginRoot(uploaded, PluginOrigin.Uploaded)],
                PluginSignatureVerifier.Create(new PluginSigningOptions(), new HostingEnvironment { EnvironmentName = Environments.Development }),
                allowUnsigned: false,
                PluginOptions.DefaultMaxViewBytes,
                NullLoggerFactory.Instance);

            services.BuildServiceProvider().GetRequiredService<PluginRegistry>().GetRefusedPlugins()
                .Should().ContainSingle(r => r.PluginId == "bad-version" && r.Rule == PluginManifestRule.VersionInvalid);
        }
        finally
        {
            Directory.Delete(uploaded, recursive: true);
        }
    }

    /// <summary>A pending upgrade record flags its plugin in the registry as soon as the loader has run.</summary>
    [Fact]
    public void PendingUpgradeRecord_FlagsThePluginAsMigrationPending()
    {
        var uploaded = NewRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(uploaded, ".upgrades"));
            File.WriteAllText(
                Path.Combine(uploaded, ".upgrades", "pending-plugin.json"),
                "{\"pluginId\":\"pending-plugin\",\"fromVersion\":\"1.0.0\",\"toVersion\":\"2.0.0\"}");
            var services = new ServiceCollection();

            PluginLoader.DiscoverAndRegister(
                services,
                [new PluginRoot(uploaded, PluginOrigin.Uploaded)],
                PluginSignatureVerifier.Create(new PluginSigningOptions(), new HostingEnvironment { EnvironmentName = Environments.Development }),
                allowUnsigned: false,
                PluginOptions.DefaultMaxViewBytes,
                NullLoggerFactory.Instance);

            var registry = services.BuildServiceProvider().GetRequiredService<PluginRegistry>();
            registry.IsMigrationPending("pending-plugin").Should().BeTrue();
            registry.IsMigrationPending("other-plugin").Should().BeFalse();
        }
        finally
        {
            Directory.Delete(uploaded, recursive: true);
        }
    }

    /// <summary>A folder that is not a plugin id (the staging areas) is never taken for a plugin, refused or not.</summary>
    [Fact]
    public void WorkingFolders_AreNeverRefusedPlugins()
    {
        var uploaded = NewRoot();
        try
        {
            WriteRefused(uploaded, ".staged");
            var services = new ServiceCollection();

            PluginLoader.DiscoverAndRegister(
                services,
                [new PluginRoot(uploaded, PluginOrigin.Uploaded)],
                PluginSignatureVerifier.Create(new PluginSigningOptions(), new HostingEnvironment { EnvironmentName = Environments.Development }),
                allowUnsigned: false,
                PluginOptions.DefaultMaxViewBytes,
                NullLoggerFactory.Instance);

            services.BuildServiceProvider().GetRequiredService<PluginRegistry>().GetRefusedPlugins().Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(uploaded, recursive: true);
        }
    }

    /// <summary>Creates an empty plugin root for one test.</summary>
    /// <returns>The absolute folder path; the test deletes it.</returns>
    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"plugin-loader-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>Writes a plugin folder whose manifest version is one the host does not read.</summary>
    /// <param name="root">The plugin root.</param>
    /// <param name="id">The plugin id, which is also the folder name.</param>
    private static void WriteRefused(string root, string id)
    {
        var folder = Path.Combine(root, id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), PluginsTestSeeds.ManifestJson(id, ",\"manifestVersion\":9"));
    }
}
