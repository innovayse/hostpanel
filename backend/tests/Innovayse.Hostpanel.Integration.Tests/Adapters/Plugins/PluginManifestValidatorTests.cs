using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Innovayse.SDK.Plugins.Manifest;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// The manifest rules every plugin meets before it loads or installs: one test per refusal rule, the version 1
/// and shipped manifests that must still pass, the SDK major check on assembly metadata, and the load path that
/// skips only the refused plugin and logs its id and the rule. No container or database.
/// </summary>
public sealed class PluginManifestValidatorTests : IDisposable
{
    /// <summary>A scratch folder per test instance, removed on disposal.</summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"manifest-rules-{Guid.NewGuid():N}");

    /// <summary>Creates the scratch folder.</summary>
    public PluginManifestValidatorTests()
    {
        Directory.CreateDirectory(_folder);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    /// <summary>Reads a version 2 manifest with the given members after the required ones.</summary>
    /// <param name="extra">Extra JSON members, each starting with a comma.</param>
    /// <returns>The manifest.</returns>
    private static PluginManifest Manifest(string extra)
    {
        return PluginManifestReader.Deserialize(PluginsTestSeeds.ManifestJson("rules-plugin", ",\"manifestVersion\":2" + extra))!;
    }

    /// <summary>Asserts that a manifest breaks exactly one given rule.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <param name="rule">The rule it must break.</param>
    private static void AssertBreaks(PluginManifest manifest, PluginManifestRule rule)
    {
        PluginManifestValidator.Validate(manifest).Should().NotBeNull().And.Match<PluginManifestViolation>(v => v.Rule == rule);
    }

    /// <summary>A manifest version this host does not read is refused, an absent one reads as 1 and passes.</summary>
    [Fact]
    public void Validate_UnknownManifestVersion_BreaksManifestVersionUnknown()
    {
        AssertBreaks(PluginManifestReader.Deserialize(PluginsTestSeeds.ManifestJson("p", ",\"manifestVersion\":3"))!, PluginManifestRule.ManifestVersionUnknown);
        AssertBreaks(PluginManifestReader.Deserialize(PluginsTestSeeds.ManifestJson("p", ",\"manifestVersion\":0"))!, PluginManifestRule.ManifestVersionUnknown);
        PluginManifestValidator.Validate(PluginManifestReader.Deserialize(PluginsTestSeeds.ManifestJson("p"))!).Should().BeNull();
    }

    /// <summary>A client route, page or slot entry may name Any or a defined client permission, and nothing else.</summary>
    [Fact]
    public void Validate_UnknownClientPermission_BreaksPermissionUnknown()
    {
        PluginManifestValidator.Validate(Manifest("""
            ,"api":{"client":[{"method":"get","path":"/a","permission":"Any"},{"method":"get","path":"/b","permission":"ViewDomains"}]},
            "ui":{"client":{"pages":[{"id":"p","path":"/p","title":"P","view":"v.json","permission":"ViewPayInvoices"}]}}
            """)).Should().BeNull();

        AssertBreaks(Manifest(""","api":{"client":[{"method":"get","path":"/a","permission":"Nonsense"}]}"""), PluginManifestRule.PermissionUnknown);
        AssertBreaks(Manifest(""","api":{"client":[{"method":"get","path":"/a","permission":"any"}]}"""), PluginManifestRule.PermissionUnknown);
        AssertBreaks(Manifest(""","api":{"client":[{"method":"get","path":"/a","permission":"None"}]}"""), PluginManifestRule.PermissionUnknown);
        AssertBreaks(Manifest(""","api":{"client":[{"method":"get","path":"/a","permission":"ViewDomains, ViewPayInvoices"}]}"""), PluginManifestRule.PermissionUnknown);
        AssertBreaks(Manifest(""","api":{"client":[{"method":"get","path":"/a","permission":"4"}]}"""), PluginManifestRule.PermissionUnknown);
        AssertBreaks(Manifest(""","ui":{"client":{"pages":[{"id":"p","path":"/p","title":"P","view":"v.json","permission":"Nonsense"}]}}"""), PluginManifestRule.PermissionUnknown);
        AssertBreaks(Manifest(""","ui":{"slots":[{"id":"s","slot":"client.dashboard.cards","view":"v.json","permission":"Nonsense"}]}"""), PluginManifestRule.PermissionUnknown);
    }

    /// <summary>An admin route, page or slot entry takes no permission, not even Any.</summary>
    [Fact]
    public void Validate_PermissionOnAdminArea_BreaksAdminPermissionDeclared()
    {
        AssertBreaks(Manifest(""","api":{"admin":[{"method":"get","path":"/a","permission":"Any"}]}"""), PluginManifestRule.AdminPermissionDeclared);
        AssertBreaks(Manifest(""","ui":{"admin":{"pages":[{"id":"p","path":"/p","title":"P","view":"v.json","permission":"ViewDomains"}]}}"""), PluginManifestRule.AdminPermissionDeclared);
        AssertBreaks(Manifest(""","ui":{"slots":[{"id":"s","slot":"admin.client.panels","view":"v.json","permission":"Any"}]}"""), PluginManifestRule.AdminPermissionDeclared);
    }

    /// <summary>A route path is lowercase segments and parameters only, with no dot segment and no empty segment.</summary>
    /// <param name="path">The declared path.</param>
    [Theory]
    [InlineData("/Upper")]
    [InlineData("/a/../b")]
    [InlineData("/a/./b")]
    [InlineData("/a//b")]
    [InlineData("/a/")]
    [InlineData("/")]
    [InlineData("")]
    [InlineData("a/b")]
    [InlineData("/a b")]
    [InlineData("/a/{Id")]
    [InlineData("/a/x{id}")]
    public void Validate_BadRoutePath_BreaksRoutePathInvalid(string path)
    {
        AssertBreaks(Manifest($$""","api":{"client":[{"method":"get","path":"{{path}}"}]}"""), PluginManifestRule.RoutePathInvalid);
    }

    /// <summary>Lowercase literals and {parameters} are valid paths.</summary>
    [Fact]
    public void Validate_GoodRoutePaths_Pass()
    {
        PluginManifestValidator.Validate(Manifest("""
            ,"api":{"client":[{"method":"get","path":"/invoices/{id}"},{"method":"get","path":"/a-b_c/d1"},{"method":"get","path":"/{id}/lines/{lineId}"}]}
            """)).Should().BeNull();
    }

    /// <summary>The same method and template twice in one area is refused, whatever the parameter is called.</summary>
    [Fact]
    public void Validate_SameMethodAndTemplateTwice_BreaksRouteDuplicate()
    {
        AssertBreaks(Manifest(""","api":{"client":[{"method":"get","path":"/a"},{"method":"get","path":"/a"}]}"""), PluginManifestRule.RouteDuplicate);
        AssertBreaks(Manifest(""","api":{"client":[{"method":"get","path":"/a/{id}"},{"method":"get","path":"/a/{name}"}]}"""), PluginManifestRule.RouteDuplicate);
        AssertBreaks(Manifest(""","api":{"admin":[{"method":"post","path":"/a"},{"method":"post","path":"/a"}]}"""), PluginManifestRule.RouteDuplicate);

        // Another method, or the other area, is a different route.
        PluginManifestValidator.Validate(Manifest(
            ""","api":{"client":[{"method":"get","path":"/a"},{"method":"post","path":"/a"}],"admin":[{"method":"get","path":"/a"}]}""")).Should().BeNull();
    }

    /// <summary>A text key that looks like a key must be in the English texts; plain text needs no entry.</summary>
    [Fact]
    public void Validate_TextKeyMissingFromEnglish_BreaksLabelKeyMissing()
    {
        AssertBreaks(Manifest(""","ui":{"client":{"menus":[{"id":"m","label":"menu.reports","page":"p"}]}}"""), PluginManifestRule.LabelKeyMissing);
        AssertBreaks(Manifest(
            ""","locales":{"ru":{"menu.reports":"R"}},"ui":{"client":{"menus":[{"id":"m","label":"menu.reports","page":"p"}]}}"""), PluginManifestRule.LabelKeyMissing);
        AssertBreaks(Manifest(""","ui":{"admin":{"pages":[{"id":"p","path":"/p","title":"page.title","view":"v.json"}]}}"""), PluginManifestRule.LabelKeyMissing);
        AssertBreaks(Manifest(""","settings":[{"key":"k","label":"setting.k","type":"text"}]"""), PluginManifestRule.LabelKeyMissing);
        AssertBreaks(Manifest(""","settings":[{"key":"k","label":"Key","type":"text","description":"setting.k.help"}]"""), PluginManifestRule.LabelKeyMissing);
        AssertBreaks(Manifest(""","ui":{"slots":[{"id":"s","slot":"client.dashboard.cards","view":"v.json","title":"card.title"}]}"""), PluginManifestRule.LabelKeyMissing);

        PluginManifestValidator.Validate(Manifest("""
            ,"locales":{"en":{"menu.reports":"Reports","setting.k":"K","card.title":"Card"}},
            "settings":[{"key":"k","label":"setting.k","type":"text"}],
            "ui":{"client":{"menus":[{"id":"m","label":"menu.reports","page":"p"},{"id":"n","label":"Plain text","page":"p"}]},
                  "slots":[{"id":"s","slot":"client.dashboard.cards","view":"v.json","title":"card.title"}]}
            """)).Should().BeNull();
    }

    /// <summary>A slot id the host does not offer is refused.</summary>
    [Fact]
    public void Validate_UnknownSlot_BreaksSlotUnknown()
    {
        AssertBreaks(Manifest(""","ui":{"slots":[{"id":"s","slot":"client.nowhere","view":"v.json"}]}"""), PluginManifestRule.SlotUnknown);
        AssertBreaks(Manifest(""","ui":{"slots":[{"id":"s","slot":"CLIENT.DASHBOARD.CARDS","view":"v.json"}]}"""), PluginManifestRule.SlotUnknown);
    }

    /// <summary>A context key must be one the named slot offers.</summary>
    [Fact]
    public void Validate_ContextKeyTheSlotDoesNotOffer_BreaksSlotContextKeyNotOffered()
    {
        AssertBreaks(Manifest(
            ""","ui":{"slots":[{"id":"s","slot":"client.dashboard.cards","view":"v.json","contextKeys":["serviceId"]}]}"""), PluginManifestRule.SlotContextKeyNotOffered);
        AssertBreaks(Manifest(
            ""","ui":{"slots":[{"id":"s","slot":"client.service.tabs","view":"v.json","contextKeys":["serviceId","clientId"]}]}"""), PluginManifestRule.SlotContextKeyNotOffered);

        PluginManifestValidator.Validate(Manifest(
            ""","ui":{"slots":[{"id":"s","slot":"admin.service.panels","view":"v.json","contextKeys":["serviceId","clientId"]}]}""")).Should().BeNull();
    }

    /// <summary>A version 1 manifest, with its legacy fields and long free-text labels, still passes.</summary>
    [Fact]
    public void Validate_VersionOneManifest_Passes()
    {
        var manifest = PluginManifestReader.Deserialize(PluginsTestSeeds.ManifestJson(
            "legacy", ",\"fields\":[{\"key\":\"u\",\"label\":\"Gateway base URL (e.g. https://pg.example.am)\",\"type\":\"text\",\"required\":true}]"))!;

        manifest.ManifestVersion.Should().Be(1);
        PluginManifestValidator.Validate(manifest).Should().BeNull();
    }

    /// <summary>An assembly built against an older SDK major version is refused from its metadata alone.</summary>
    [Fact]
    public void ValidateSdkReference_OlderMajor_BreaksSdkAssemblyOutdated()
    {
        File.WriteAllBytes(Path.Combine(_folder, "Old.Plugin.dll"), PluginAssemblyFixtures.BuiltAgainstSdk(new Version(1, 4, 0, 0)));

        var violation = PluginManifestValidator.ValidateSdkReference(_folder);

        violation.Should().NotBeNull();
        violation!.Rule.Should().Be(PluginManifestRule.SdkAssemblyOutdated);
        violation.Detail.Should().Contain("Old.Plugin.dll").And.Contain("1.4").And.Contain("rebuild");
    }

    /// <summary>The host's own SDK major, a newer minor, native files and an empty folder all pass.</summary>
    [Fact]
    public void ValidateSdkReference_CurrentMajorOrNotManaged_Passes()
    {
        var hostVersion = typeof(PluginManifest).Assembly.GetName().Version!;
        File.WriteAllBytes(Path.Combine(_folder, "Current.dll"), PluginAssemblyFixtures.BuiltAgainstSdk(hostVersion));
        File.WriteAllBytes(Path.Combine(_folder, "Newer.dll"), PluginAssemblyFixtures.BuiltAgainstSdk(new Version(hostVersion.Major, hostVersion.Minor + 3, 0, 0)));
        File.WriteAllBytes(Path.Combine(_folder, "native.dll"), [0x4D, 0x5A, 0x00, 0x01, 0x02]);

        PluginManifestValidator.ValidateSdkReference(_folder).Should().BeNull();
        PluginManifestValidator.ValidateSdkReference(Directory.CreateDirectory(Path.Combine(_folder, "empty")).FullName).Should().BeNull();
    }

    /// <summary>The metadata is read without loading the plugin assembly into the process.</summary>
    [Fact]
    public void ValidateSdkReference_DoesNotLoadTheAssembly()
    {
        File.WriteAllBytes(Path.Combine(_folder, "Fixture.Plugin.dll"), PluginAssemblyFixtures.BuiltAgainstSdk(new Version(1, 0, 0, 0)));

        PluginManifestValidator.ValidateSdkReference(_folder);

        AppDomain.CurrentDomain.GetAssemblies().Should().NotContain(a => a.GetName().Name == "Fixture.Plugin");
    }

    /// <summary>Every manifest the build ships passes the rules at load.</summary>
    /// <param name="project">The shipped provider project folder name.</param>
    [Theory]
    [InlineData("Innovayse.Providers.CWP")]
    [InlineData("Innovayse.Providers.CWP7")]
    [InlineData("Innovayse.Providers.Inecobank")]
    public void TryRead_ShippedManifest_Loads(string project)
    {
        var manifestPath = ShippedManifestPath(project);

        var manifest = new PluginManifestReader(new CapturingLogger<PluginManifestReader>()).TryRead(manifestPath);

        manifest.Should().NotBeNull();
    }

    /// <summary>A shipped plugin's real compiled assembly, built against the SDK project, passes the SDK major check.</summary>
    [Fact]
    public void ValidateSdkReference_ShippedPluginAssembly_Passes()
    {
        var shipped = Path.Combine(AppContext.BaseDirectory, "Innovayse.Providers.CWP7.dll");
        File.Exists(shipped).Should().BeTrue("the test project references the CWP7 provider");
        File.Copy(shipped, Path.Combine(_folder, "Innovayse.Providers.CWP7.dll"));

        PluginManifestValidator.ValidateSdkReference(_folder).Should().BeNull();
    }

    /// <summary>A refused plugin is skipped at load with a log line naming its id and the rule.</summary>
    [Fact]
    public void TryRead_BrokenRule_SkipsThePluginAndLogsItsIdAndTheRule()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_folder, "rules-plugin")).FullName;
        File.WriteAllText(
            Path.Combine(folder, "plugin.json"),
            PluginsTestSeeds.ManifestJson("rules-plugin", ",\"manifestVersion\":2,\"api\":{\"admin\":[{\"method\":\"get\",\"path\":\"/a\",\"permission\":\"Any\"}]}"));
        var logger = new CapturingLogger<PluginManifestReader>();

        var manifest = new PluginManifestReader(logger).TryRead(Path.Combine(folder, "plugin.json"));

        manifest.Should().BeNull();
        logger.Entries.Should().ContainSingle().Which.Should().Match<(LogLevel Level, string Message)>(
            e => e.Level == LogLevel.Warning
                && e.Message.Contains("rules-plugin")
                && e.Message.Contains(nameof(PluginManifestRule.AdminPermissionDeclared)));
    }

    /// <summary>A plugin built against an older SDK is skipped at load and the log says to rebuild it.</summary>
    [Fact]
    public void TryRead_PluginBuiltAgainstOlderSdk_SkipsThePluginAndLogsTheRule()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_folder, "old-plugin")).FullName;
        File.WriteAllText(Path.Combine(folder, "plugin.json"), PluginsTestSeeds.ManifestJson("old-plugin"));
        File.WriteAllBytes(Path.Combine(folder, "Old.Plugin.dll"), PluginAssemblyFixtures.BuiltAgainstSdk(new Version(1, 4, 0, 0)));
        var logger = new CapturingLogger<PluginManifestReader>();

        new PluginManifestReader(logger).TryRead(Path.Combine(folder, "plugin.json")).Should().BeNull();

        logger.Messages.Should().ContainSingle().Which
            .Should().Contain("old-plugin").And.Contain(nameof(PluginManifestRule.SdkAssemblyOutdated)).And.Contain("rebuild");
    }

    /// <summary>Finds a shipped provider's <c>plugin.json</c> in the source tree above the test output.</summary>
    /// <param name="project">The provider project folder name.</param>
    /// <returns>The absolute path of the manifest.</returns>
    private static string ShippedManifestPath(string project)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", project, "plugin.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"The manifest of {project} was not found above {AppContext.BaseDirectory}.");
    }
}
