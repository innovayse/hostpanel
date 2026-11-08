using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Hostpanel.Sdk.Plugins.Manifest;
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

    /// <summary>
    /// Reads a version 2 app manifest with the given members after the required ones. The type is App so the rules
    /// under test are reached: any other type with api or ui blocks breaks EndpointsNotDeclared first (#818).
    /// </summary>
    /// <param name="extra">Extra JSON members, each starting with a comma.</param>
    /// <returns>The manifest.</returns>
    private static PluginManifest Manifest(string extra)
    {
        return PluginManifestReader.Deserialize(PluginsTestSeeds.ManifestJson("rules-plugin", ",\"manifestVersion\":2" + extra, "App"))!;
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

    /// <summary>A plugin version that is not a strict semantic version breaks the version rule.</summary>
    /// <param name="version">The version text in the manifest.</param>
    [Theory]
    [InlineData("")]
    [InlineData("1.0")]
    [InlineData("01.0.0")]
    [InlineData("1.0.0.0")]
    [InlineData("v1.0.0")]
    [InlineData("1.0.0+")]
    [InlineData("4294967296.0.0")]
    public void Validate_InvalidPluginVersion_BreaksVersionInvalid(string version)
    {
        var json = PluginsTestSeeds.ManifestJson("p").Replace("\"version\": \"1.0.0\"", $"\"version\": \"{version}\"");

        AssertBreaks(PluginManifestReader.Deserialize(json)!, PluginManifestRule.VersionInvalid);
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
        AssertBreaks(
            Manifest(""","locales":{"ru":{"setting.k.help":"H"}},"settings":[{"key":"k","label":"Key","type":"text","description":"setting.k.help"}]"""),
            PluginManifestRule.LabelKeyMissing);
        AssertBreaks(
            Manifest(""","locales":{"ru":{"setting.k.hint":"H"}},"settings":[{"key":"k","label":"Key","type":"text","placeholder":"setting.k.hint"}]"""),
            PluginManifestRule.LabelKeyMissing);
        AssertBreaks(Manifest(""","ui":{"slots":[{"id":"s","slot":"client.dashboard.cards","view":"v.json","title":"card.title"}]}"""), PluginManifestRule.LabelKeyMissing);

        PluginManifestValidator.Validate(Manifest("""
            ,"locales":{"en":{"menu.reports":"Reports","setting.k":"K","card.title":"Card"}},
            "settings":[{"key":"k","label":"setting.k","type":"text"}],
            "ui":{"client":{"menus":[{"id":"m","label":"menu.reports","page":"p"},{"id":"n","label":"Plain text","page":"p"}]},
                  "slots":[{"id":"s","slot":"client.dashboard.cards","view":"v.json","title":"card.title"}]}
            """)).Should().BeNull();
    }

    /// <summary>
    /// A setting description or placeholder shaped like a key but defined in no locale is literal text (a host name, a
    /// version), not a missing key; a real key of the manifest's texts in one of them is checked like a label.
    /// </summary>
    /// <param name="value">The description or placeholder.</param>
    [Theory]
    [InlineData("example.com")]
    [InlineData("api.example.org")]
    [InlineData("v1.0")]
    [InlineData("https://pg.example.am/v1")]
    public void Validate_HelpTextShapedLikeAKeyButDefinedNowhere_Passes(string value)
    {
        PluginManifestValidator.Validate(Manifest(
            $$""","settings":[{"key":"k","label":"Key","type":"text","description":"{{value}}","placeholder":"{{value}}"}]""")).Should().BeNull();
        PluginManifestValidator.Validate(Manifest(
            $$$""","locales":{"en":{"other.key":"O"}},"settings":[{"key":"k","label":"Key","type":"text","placeholder":"{{{value}}}"}]""")).Should().BeNull();
    }

    /// <summary>A description or placeholder that is a defined key passes once English defines it too.</summary>
    [Fact]
    public void Validate_HelpTextThatIsADefinedKey_PassesWhenEnglishDefinesIt()
    {
        PluginManifestValidator.Validate(Manifest("""
            ,"locales":{"en":{"setting.k.help":"Help"},"ru":{"setting.k.help":"Pomosh"}},
            "settings":[{"key":"k","label":"Key","type":"text","description":"setting.k.help"}]
            """)).Should().BeNull();
    }

    /// <summary>
    /// A trailing newline never passes a text key or path rule: a label "menu.reports" followed by a newline is
    /// plain text (not a key), and a path with a newline is invalid.
    /// </summary>
    [Fact]
    public void Validate_TrailingNewline_IsNotPartOfAKeyOrAPath()
    {
        // The newline makes it plain text, so no English entry is needed; the key without it still is.
        PluginManifestValidator.Validate(Manifest(""","ui":{"client":{"menus":[{"id":"m","label":"menu.reports\n","page":"p"}]}}""")).Should().BeNull();
        AssertBreaks(Manifest(""","ui":{"client":{"menus":[{"id":"m","label":"menu.reports","page":"p"}]}}"""), PluginManifestRule.LabelKeyMissing);
    }

    /// <summary>
    /// A menu icon that is not a plain icon name (a URL, markup, upper case, too long, empty or ending in a newline) breaks
    /// the icon rule in either area.
    /// </summary>
    /// <param name="icon">The icon as written in the manifest JSON, with <c>\n</c> as a JSON escape.</param>
    /// <param name="area">The manifest area member the menu is declared in.</param>
    [Theory]
    [InlineData("https://evil.example/x.svg", "client")]
    [InlineData("<svg onload=alert(1)>", "client")]
    [InlineData("Home", "admin")]
    [InlineData("chart_bar", "admin")]
    [InlineData("", "client")]
    [InlineData("home\\n", "client")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "admin")]
    public void Validate_MenuIconNotAPlainName_BreaksMenuIconInvalid(string icon, string area)
    {
        AssertBreaks(
            Manifest($$$""","ui":{"{{{area}}}":{"menus":[{"id":"m","label":"Reports","page":"p","icon":"{{{icon}}}"}]}}"""),
            PluginManifestRule.MenuIconInvalid);
    }

    /// <summary>A menu icon of lowercase letters, digits and hyphens up to 64 characters passes, and so does a menu with no icon.</summary>
    /// <param name="icon">The icon as written in the manifest JSON.</param>
    [Theory]
    [InlineData("chart-bar")]
    [InlineData("x")]
    [InlineData("icon-2")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Validate_MenuIconPlainName_Passes(string icon)
    {
        PluginManifestValidator.Validate(
            Manifest($$$""","ui":{"client":{"menus":[{"id":"m","label":"Reports","page":"p","icon":"{{{icon}}}"}]}}""")).Should().BeNull();
        PluginManifestValidator.Validate(
            Manifest(""","ui":{"client":{"menus":[{"id":"m","label":"Reports","page":"p"}]}}""")).Should().BeNull();
    }

    /// <summary>The icon rule maps to its own error code.</summary>
    [Fact]
    public void CodeFor_MenuIconInvalid_IsPluginMenuIconInvalid()
    {
        PluginPackageErrors.CodeFor(PluginManifestRule.MenuIconInvalid).Should().Be(HostpanelErrorCodes.PLUGIN_MENU_ICON_INVALID);
    }

    /// <summary>A page id the page lookup would never serve is refused at install, in either area.</summary>
    /// <param name="id">The page id as written in the manifest JSON, with <c>\n</c> as a JSON escape.</param>
    /// <param name="area">The panel the page is declared in.</param>
    [Theory]
    [InlineData("Reports", "client")]
    [InlineData("Reports", "admin")]
    [InlineData("", "client")]
    [InlineData("-a", "admin")]
    [InlineData("_a", "client")]
    [InlineData("a.b", "client")]
    [InlineData("a/b", "admin")]
    [InlineData("with space", "client")]
    [InlineData("reports\\n", "admin")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "client")]
    public void Validate_PageIdNotAPageIdentifier_BreaksPageIdInvalid(string id, string area)
    {
        AssertBreaks(
            Manifest($$$""","ui":{"{{{area}}}":{"pages":[{"id":"{{{id}}}","path":"/p","title":"P","view":"v.json"}]}}"""),
            PluginManifestRule.PageIdInvalid);
    }

    /// <summary>A page id of lowercase letters, digits, hyphens and underscores up to 64 characters passes.</summary>
    /// <param name="id">The page id.</param>
    [Theory]
    [InlineData("reports")]
    [InlineData("monthly-report_2")]
    [InlineData("0")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Validate_PageIdPageIdentifier_Passes(string id)
    {
        PluginManifestValidator.Validate(
            Manifest($$$""","ui":{"client":{"pages":[{"id":"{{{id}}}","path":"/p","title":"P","view":"v.json"}]}}""")).Should().BeNull();
    }

    /// <summary>The same page id twice in one area is refused, while the same id once per area passes.</summary>
    [Fact]
    public void Validate_SamePageIdTwiceInOneArea_BreaksPageIdInvalid()
    {
        AssertBreaks(
            Manifest(""","ui":{"client":{"pages":[{"id":"p","path":"/a","title":"A","view":"a.json"},{"id":"p","path":"/b","title":"B","view":"b.json"}]}}"""),
            PluginManifestRule.PageIdInvalid);
        AssertBreaks(
            Manifest(""","ui":{"admin":{"pages":[{"id":"p","path":"/a","title":"A","view":"a.json"},{"id":"p","path":"/b","title":"B","view":"b.json"}]}}"""),
            PluginManifestRule.PageIdInvalid);
        PluginManifestValidator.Validate(Manifest("""
            ,"ui":{"client":{"pages":[{"id":"p","path":"/a","title":"A","view":"a.json"}]},"admin":{"pages":[{"id":"p","path":"/a","title":"A","view":"a.json"}]}}
            """)).Should().BeNull();
    }

    /// <summary>The page id rule maps to its own error code.</summary>
    [Fact]
    public void CodeFor_PageIdInvalid_IsPluginPageIdInvalid()
    {
        PluginPackageErrors.CodeFor(PluginManifestRule.PageIdInvalid).Should().Be(HostpanelErrorCodes.PLUGIN_PAGE_ID_INVALID);
    }

    /// <summary>A route path that ends in a newline is invalid, literal or parameter segment alike.</summary>
    /// <param name="path">The path as written in the manifest JSON, with <c>\n</c> as a JSON escape.</param>
    [Theory]
    [InlineData("/a\\n")]
    [InlineData("/a/{id}\\n")]
    [InlineData("/a\\n/b")]
    public void Validate_RoutePathWithTrailingNewline_BreaksRoutePathInvalid(string path)
    {
        AssertBreaks(Manifest($$""","api":{"client":[{"method":"get","path":"{{path}}"}]}"""), PluginManifestRule.RoutePathInvalid);
    }

    /// <summary>A path with a trailing newline is not silently a distinct route next to the same path without it: the rule refuses it.</summary>
    [Fact]
    public void Validate_RouteAndTheSameRouteWithTrailingNewline_BreaksRoutePathInvalid()
    {
        AssertBreaks(Manifest(""","api":{"client":[{"method":"get","path":"/a"},{"method":"get","path":"/a\n"}]}"""), PluginManifestRule.RoutePathInvalid);
        AssertBreaks(Manifest(""","api":{"client":[{"method":"get","path":"/a\n"},{"method":"get","path":"/a"}]}"""), PluginManifestRule.RoutePathInvalid);
    }

    /// <summary>A page path follows the route path rule in both areas.</summary>
    /// <param name="path">The page path as written in the manifest JSON.</param>
    [Theory]
    [InlineData("/Upper")]
    [InlineData("/a/../b")]
    [InlineData("/a//b")]
    [InlineData("a")]
    [InlineData("/")]
    [InlineData("/a\\n")]
    public void Validate_BadPagePath_BreaksRoutePathInvalid(string path)
    {
        AssertBreaks(
            Manifest($$$""","ui":{"client":{"pages":[{"id":"p","path":"{{{path}}}","title":"P","view":"v.json"}]}}"""), PluginManifestRule.RoutePathInvalid);
        AssertBreaks(
            Manifest($$$""","ui":{"admin":{"pages":[{"id":"p","path":"{{{path}}}","title":"P","view":"v.json"}]}}"""), PluginManifestRule.RoutePathInvalid);
    }

    /// <summary>Lowercase literals and {parameters} are valid page paths.</summary>
    [Fact]
    public void Validate_GoodPagePaths_Pass()
    {
        PluginManifestValidator.Validate(Manifest("""
            ,"ui":{"client":{"pages":[{"id":"p","path":"/reports/{id}","title":"P","view":"v.json"}]},
                  "admin":{"pages":[{"id":"q","path":"/a-b_c","title":"Q","view":"v.json"}]}}
            """)).Should().BeNull();
    }

    /// <summary>A widget id that is not lowercase letters, digits, hyphen and underscore is refused.</summary>
    /// <param name="id">The widget id.</param>
    [Theory]
    [InlineData("Tab")]
    [InlineData("a:b")]
    [InlineData("a,b")]
    [InlineData("-x")]
    [InlineData("a b")]
    public void Validate_UnsafeWidgetId_BreaksSlotWidgetIdInvalid(string id)
    {
        AssertBreaks(Manifest($$""","ui":{"slots":[{"id":"{{id}}","slot":"client.dashboard.cards","view":"v.json"}]}"""), PluginManifestRule.SlotWidgetIdInvalid);
    }

    /// <summary>The same widget id twice for one slot is refused, while one id in two slots is fine.</summary>
    [Fact]
    public void Validate_DuplicateWidgetIdInOneSlot_BreaksSlotWidgetDuplicate()
    {
        AssertBreaks(Manifest(""","ui":{"slots":[{"id":"w","slot":"client.dashboard.cards","view":"v.json"},{"id":"w","slot":"client.dashboard.cards","view":"v.json"}]}"""), PluginManifestRule.SlotWidgetDuplicate);
        PluginManifestValidator.Validate(Manifest(""","ui":{"slots":[{"id":"w","slot":"client.dashboard.cards","view":"v.json"},{"id":"w","slot":"client.service.tabs","view":"v.json"}]}""")).Should().BeNull();
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

    /// <summary>An assembly the host cannot read is refused with its own rule, not an exception that would become a server error.</summary>
    [Fact]
    public void ValidateSdkReference_UnreadableAssembly_BreaksAssemblyUnreadable()
    {
        var path = Path.Combine(_folder, "Locked.Plugin.dll");
        File.WriteAllBytes(path, PluginAssemblyFixtures.BuiltAgainstSdk(new Version(1, 4, 0, 0)));

        // An exclusive handle makes the host's own open fail with an IOException, as a file held by another process would.
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        var violation = PluginManifestValidator.ValidateSdkReference(_folder);

        violation.Should().NotBeNull();
        violation!.Rule.Should().Be(PluginManifestRule.AssemblyUnreadable);
        violation.Detail.Should().Contain("Locked.Plugin.dll").And.Contain(nameof(IOException));
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
            PluginsTestSeeds.ManifestJson("rules-plugin", ",\"manifestVersion\":2,\"api\":{\"admin\":[{\"method\":\"get\",\"path\":\"/a\",\"permission\":\"Any\"}]}", "App"));
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

    /// <summary>A client page may ask for the workspace layout or the standard one, or for none.</summary>
    /// <param name="layout">The layout member, or empty for none.</param>
    [Theory]
    [InlineData("")]
    [InlineData(""","layout":"standard" """)]
    [InlineData(""","layout":"workspace" """)]
    public void Validate_ClientPageLayoutKnown_Passes(string layout)
    {
        var manifest = Manifest($$$""","ui":{"client":{"pages":[{"id":"p","path":"/p","title":"P","view":"views/p.json","permission":"Any"{{{layout}}}}]}}""");

        PluginManifestValidator.Validate(manifest).Should().BeNull();
    }

    /// <summary>A layout outside the SDK's list, in any other case too, breaks PageLayoutUnknown.</summary>
    /// <param name="layout">The layout value.</param>
    [Theory]
    [InlineData("full")]
    [InlineData("Workspace")]
    [InlineData("")]
    public void Validate_ClientPageLayoutUnknown_BreaksPageLayoutUnknown(string layout)
    {
        AssertBreaks(
            Manifest($$$""","ui":{"client":{"pages":[{"id":"p","path":"/p","title":"P","view":"views/p.json","permission":"Any","layout":"{{{layout}}}"}]}}"""),
            PluginManifestRule.PageLayoutUnknown);
    }

    /// <summary>Any layout on an admin page breaks PageLayoutUnknown: the admin panel has none to honour.</summary>
    [Fact]
    public void Validate_AdminPageLayout_BreaksPageLayoutUnknown()
    {
        AssertBreaks(
            Manifest(""","ui":{"admin":{"pages":[{"id":"p","path":"/p","title":"P","view":"views/p.json","layout":"workspace"}]}}"""),
            PluginManifestRule.PageLayoutUnknown);
    }

    /// <summary>A manifest without tags, and one with well-formed tags, passes: tags are optional keywords.</summary>
    [Fact]
    public void Validate_NoTagsOrGoodTags_Passes()
    {
        PluginManifestValidator.Validate(Manifest(string.Empty)).Should().BeNull();
        PluginManifestValidator.Validate(Manifest(""","tags":["billing","pay-2","ab"]""")).Should().BeNull();
    }

    /// <summary>A tag that is too short, too long, upper case, led by a hash, outside the allowed characters or listed twice, or a list over ten tags, breaks the tags rule.</summary>
    /// <param name="tags">The tags list as written in the manifest JSON.</param>
    [Theory]
    [InlineData("""["a"]""")]
    [InlineData("""["abcdefghijklmnopqrstuvwxyz01234"]""")]
    [InlineData("""["Billing"]""")]
    [InlineData("""["#billing"]""")]
    [InlineData("""["pay ment"]""")]
    [InlineData("""["billing","billing"]""")]
    [InlineData("""["t01","t02","t03","t04","t05","t06","t07","t08","t09","t10","t11"]""")]
    public void Validate_BadTags_BreaksTagsInvalid(string tags)
    {
        AssertBreaks(Manifest($$$""","tags":{{{tags}}}"""), PluginManifestRule.TagsInvalid);
    }

    /// <summary>The tags rule maps to its own error code.</summary>
    [Fact]
    public void CodeFor_TagsInvalid_IsPluginManifestTagsInvalid()
    {
        PluginPackageErrors.CodeFor(PluginManifestRule.TagsInvalid).Should().Be(HostpanelErrorCodes.PLUGIN_MANIFEST_TAGS_INVALID);
    }
}
