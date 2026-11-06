using System.Text;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Ui;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// The view document rules every plugin meets at load and install: one test per refusal rule, a valid document
/// returned by path, and a document shared by two areas that is checked against each. Temp folders only; no container or database.
/// </summary>
public sealed class PluginViewDocumentReaderTests : IDisposable
{
    /// <summary>
    /// The view size cap the tests read under, in bytes (256 KiB): far above every document written here, so only the
    /// size tests, which pass their own cap, ever meet it.
    /// </summary>
    private const long MaxViewBytes = 256 * 1024;

    /// <summary>The deepest nesting the reader accepts, the root counted as one level; the documented reader bound.</summary>
    private const int MaxJsonDepth = 32;

    /// <summary>The nesting levels the depth documents spend before their padding: the root, <c>blocks</c> and the block.</summary>
    private const int DepthBeforePadding = 3;

    /// <summary>The longest plain icon name accepted, in characters.</summary>
    private const int MaxIconNameLength = 64;

    /// <summary>A view holding one valid preview, reading GET /site.</summary>
    private const string ValidPreviewDocument =
        """{"schema":1,"blocks":[{"kind":"preview","sources":[{"id":"a","label":"A","route":{"method":"get","path":"/site"},"field":"url"}]}]}""";

    /// <summary>A scratch plugin folder per test instance, removed on disposal.</summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"view-rules-{Guid.NewGuid():N}");

    /// <summary>Creates the scratch folder.</summary>
    public PluginViewDocumentReaderTests()
    {
        Directory.CreateDirectory(_folder);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    /// <summary>Builds a version 2 manifest with an English locale and the given extra members.</summary>
    /// <param name="extra">Extra JSON members, each starting with a comma.</param>
    /// <returns>The manifest.</returns>
    private static PluginManifest Manifest(string extra)
    {
        return PluginManifestReader.Deserialize(PluginsTestSeeds.ManifestJson(
            "view-plugin", ",\"manifestVersion\":2,\"locales\":{\"en\":{\"pages.title\":\"Title\"}}" + extra))!;
    }

    /// <summary>A manifest whose admin page shows <c>views/a.json</c> and whose admin api declares POST /publish.</summary>
    /// <returns>The manifest.</returns>
    private static PluginManifest AdminPageManifest()
    {
        return Manifest(""","api":{"admin":[{"method":"post","path":"/publish"}]},"ui":{"admin":{"pages":[{"id":"overview","path":"/o","title":"O","view":"views/a.json"}]}}""");
    }

    /// <summary>Writes a view file under the scratch folder.</summary>
    /// <param name="path">The package-relative path.</param>
    /// <param name="json">The file text.</param>
    private void Write(string path, string json)
    {
        var full = Path.Combine(_folder, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, json);
    }

    /// <summary>Asserts that reading breaks one given rule.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <param name="rule">The rule it must break.</param>
    private void AssertBreaks(PluginManifest manifest, PluginManifestRule rule)
    {
        var read = PluginViewDocumentReader.Read(_folder, manifest, MaxViewBytes);
        read.Violation.Should().NotBeNull();
        read.Violation!.Rule.Should().Be(rule);
        read.Views.Paths.Should().BeEmpty();
    }

    /// <summary>A manifest with no pages or slots reads nothing and passes.</summary>
    [Fact]
    public void Read_NoPagesOrSlots_ReturnsNone()
    {
        var read = PluginViewDocumentReader.Read(_folder, Manifest(""), MaxViewBytes);

        read.Violation.Should().BeNull();
        read.Views.Paths.Should().BeEmpty();
    }

    /// <summary>A valid document is accepted and found by the path the page names.</summary>
    [Fact]
    public void Read_ValidDocument_IsFoundByPath()
    {
        Write("views/a.json", """{"schema":1,"blocks":[{"kind":"header","title":"pages.title"},{"kind":"action","label":"Publish","route":{"method":"post","path":"/publish"}}]}""");

        var read = PluginViewDocumentReader.Read(_folder, AdminPageManifest(), MaxViewBytes);

        read.Violation.Should().BeNull();
        read.Views.Find("views/a.json")!.Value.GetProperty("schema").GetInt32().Should().Be(1);
        read.Views.Find("views/other.json").Should().BeNull();
    }

    /// <summary>A path outside the package, a path that escapes the folder and an absent file each break ViewMissing.</summary>
    [Fact]
    public void Read_PathNotPackageRelativeOrAbsent_BreaksViewMissing()
    {
        AssertBreaks(AdminPageManifest(), PluginManifestRule.ViewMissing);

        foreach (var path in new[] { "../a.json", "views/../a.json", "/etc/a.json", "views\\a.json", "views/A.json", "views/a.txt" })
        {
            AssertBreaks(Manifest($$$"""
                ,"ui":{"admin":{"pages":[{"id":"overview","path":"/o","title":"O","view":"{{{path.Replace("\\", "\\\\")}}}"}]}}
                """), PluginManifestRule.ViewMissing);
        }
    }

    /// <summary>Text that is not JSON, a root that is not an object, a missing schema or blocks, a bad kind or a bad route each break ViewInvalid.</summary>
    [Fact]
    public void Read_MalformedDocument_BreaksViewInvalid()
    {
        foreach (var json in new[]
        {
            "not json",
            "[]",
            """{"blocks":[]}""",
            """{"schema":"1","blocks":[]}""",
            """{"schema":1}""",
            """{"schema":1,"blocks":{}}""",
            """{"schema":1,"blocks":[{"kind":3}]}""",
            """{"schema":1,"blocks":[{"kind":"action","route":"/publish"}]}""",
            """{"schema":1,"blocks":[{"kind":"action","route":{"method":"post"}}]}""",
            """{"schema":1,"blocks":[{"kind":"action","route":{"method":"nope","path":"/publish"}}]}""",
        })
        {
            Write("views/a.json", json);
            AssertBreaks(AdminPageManifest(), PluginManifestRule.ViewInvalid);
        }
    }

    /// <summary>A schema other than the one this host reads breaks ViewSchemaUnsupported.</summary>
    [Fact]
    public void Read_OtherSchema_BreaksViewSchemaUnsupported()
    {
        Write("views/a.json", """{"schema":2,"blocks":[]}""");

        AssertBreaks(AdminPageManifest(), PluginManifestRule.ViewSchemaUnsupported);
    }

    /// <summary>A block kind outside the list, at any depth and compared exactly, breaks ViewBlockKindUnknown.</summary>
    [Fact]
    public void Read_UnknownBlockKind_BreaksViewBlockKindUnknown()
    {
        Write("views/a.json", """{"schema":1,"blocks":[{"kind":"tabs","tabs":[{"title":"T","blocks":[{"kind":"Header"}]}]}]}""");

        AssertBreaks(AdminPageManifest(), PluginManifestRule.ViewBlockKindUnknown);
    }

    /// <summary>A label shaped like a text key that English lacks breaks LabelKeyMissing; plain text does not.</summary>
    [Fact]
    public void Read_LabelKeyMissingInEnglish_BreaksLabelKeyMissing()
    {
        Write("views/a.json", """{"schema":1,"blocks":[{"kind":"header","title":"Plain text"},{"kind":"text","text":"pages.missing"}]}""");

        AssertBreaks(AdminPageManifest(), PluginManifestRule.LabelKeyMissing);
    }

    /// <summary>A route the area's api does not declare breaks ViewRouteUndeclared.</summary>
    [Fact]
    public void Read_RouteNotDeclaredInArea_BreaksViewRouteUndeclared()
    {
        Write("views/a.json", """{"schema":1,"blocks":[{"kind":"action","label":"Go","route":{"method":"get","path":"/publish"}}]}""");

        AssertBreaks(AdminPageManifest(), PluginManifestRule.ViewRouteUndeclared);
    }

    /// <summary>A document shown by a client page and an admin page is checked against both areas, so a route declared in only one is refused.</summary>
    [Fact]
    public void Read_SharedDocumentRouteInOneAreaOnly_BreaksViewRouteUndeclared()
    {
        Write("views/a.json", """{"schema":1,"blocks":[{"kind":"action","label":"Go","route":{"method":"post","path":"/publish"}}]}""");

        AssertBreaks(
            Manifest(""","api":{"admin":[{"method":"post","path":"/publish"}]},"ui":{"client":{"pages":[{"id":"c","path":"/c","title":"C","view":"views/a.json"}]},"admin":{"pages":[{"id":"a","path":"/a","title":"A","view":"views/a.json"}]}}"""),
            PluginManifestRule.ViewRouteUndeclared);
    }

    /// <summary>A block, tab or header action that is not an object with a string kind breaks ViewInvalid, at every nesting level.</summary>
    [Fact]
    public void Read_BlockStructureBroken_BreaksViewInvalid()
    {
        foreach (var json in new[]
        {
            """{"schema":1,"blocks":["header"]}""",
            """{"schema":1,"blocks":[{"title":"No kind"}]}""",
            """{"schema":1,"blocks":[{"kind":"tabs","tabs":{}}]}""",
            """{"schema":1,"blocks":[{"kind":"tabs","tabs":["T"]}]}""",
            """{"schema":1,"blocks":[{"kind":"tabs","tabs":[{"title":"T","blocks":{}}]}]}""",
            """{"schema":1,"blocks":[{"kind":"tabs","tabs":[{"title":"T","blocks":[{"title":"No kind"}]}]}]}""",
            """{"schema":1,"blocks":[{"kind":"header","actions":{}}]}""",
            """{"schema":1,"blocks":[{"kind":"header","actions":[{"label":"No kind"}]}]}""",
        })
        {
            Write("views/a.json", json);
            AssertBreaks(AdminPageManifest(), PluginManifestRule.ViewInvalid);
        }
    }

    /// <summary>Well-formed tabs and header actions are accepted.</summary>
    [Fact]
    public void Read_NestedTabsAndActions_AreAccepted()
    {
        Write("views/a.json", """{"schema":1,"blocks":[{"kind":"header","title":"T","actions":[{"kind":"link","label":"Docs","href":"/docs"}]},{"kind":"tabs","tabs":[{"title":"One","blocks":[{"kind":"text","text":"Hi"}]},{"title":"Empty"}]}]}""");

        var read = PluginViewDocumentReader.Read(_folder, AdminPageManifest(), MaxViewBytes);

        read.Violation.Should().BeNull();
    }

    /// <summary>A link that is a script, data or protocol-relative URL, carries user info, a backslash or a control character, or is not a string breaks ViewInvalid.</summary>
    [Fact]
    public void Read_UnsafeLink_BreaksViewInvalid()
    {
        foreach (var href in new[]
        {
            "\"javascript:alert(1)\"",
            "\"JavaScript:alert(1)\"",
            "\"data:text/html,<script>alert(1)</script>\"",
            "\"vbscript:msgbox\"",
            "\"http://example.com/\"",
            "\"//evil.example/\"",
            "\"/\\\\evil.example/\"",
            "\"https://user:pass@evil.example/\"",
            "\"https://evil.example\\\\@good.example/\"",
            "\"/docs\\u0000\"",
            "\"java\\tscript:alert(1)\"",
            "\"docs\"",
            "\"\"",
            "1",
            "{\"path\":\"/docs\"}",
        })
        {
            Write("views/a.json", $$"""{"schema":1,"blocks":[{"kind":"link","label":"Go","href":{{href}}}]}""");
            AssertBreaks(AdminPageManifest(), PluginManifestRule.ViewInvalid);

            Write("views/a.json", $$"""{"schema":1,"blocks":[{"kind":"tabs","tabs":[{"title":"T","blocks":[{"kind":"table","rows":[{"url":{{href}}}]}]}]}]}""");
            AssertBreaks(AdminPageManifest(), PluginManifestRule.ViewInvalid);
        }
    }

    /// <summary>A host-relative path and an absolute https URL are accepted as links.</summary>
    [Fact]
    public void Read_SafeLink_IsAccepted()
    {
        foreach (var href in new[] { "/docs", "/services/1/site?tab=a#b", "https://docs.example.com/plugins", "HTTPS://docs.example.com/" })
        {
            Write("views/a.json", $$"""{"schema":1,"blocks":[{"kind":"link","label":"Go","href":"{{href}}"},{"kind":"text","text":"x","url":"{{href}}"}]}""");

            var read = PluginViewDocumentReader.Read(_folder, AdminPageManifest(), MaxViewBytes);

            read.Violation.Should().BeNull(href);
        }
    }

    /// <summary>
    /// Markup in a label is not rejected or rewritten: the reader keeps it as data, and the panel renders labels as
    /// text, never as HTML, so the string reaches the document unchanged.
    /// </summary>
    [Fact]
    public void Read_MarkupInLabel_IsPassedThroughAsText()
    {
        const string Markup = "<img src=x onerror=alert(1)><script>alert(1)</script>";
        Write("views/a.json", $$"""{"schema":1,"blocks":[{"kind":"text","text":"{{Markup}}"}]}""");

        var read = PluginViewDocumentReader.Read(_folder, AdminPageManifest(), MaxViewBytes);

        read.Violation.Should().BeNull();
        read.Views.Find("views/a.json")!.Value.GetProperty("blocks")[0].GetProperty("text").GetString().Should().Be(Markup);
    }

    /// <summary>A document one byte over the cap is refused with ViewInvalid before it is parsed.</summary>
    [Fact]
    public void Read_DocumentOverSizeCap_BreaksViewInvalid()
    {
        const string Json = """{"schema":1,"blocks":[{"kind":"text","text":"Hi"}]}""";
        Write("views/a.json", Json);
        var cap = Encoding.UTF8.GetByteCount(Json) - 1;

        var read = PluginViewDocumentReader.Read(_folder, AdminPageManifest(), cap);

        read.Violation.Should().NotBeNull();
        read.Violation!.Rule.Should().Be(PluginManifestRule.ViewInvalid);
        read.Violation.Detail.Should().Contain("size limit");
        read.Views.Paths.Should().BeEmpty();
    }

    /// <summary>A document exactly at the cap is read and accepted.</summary>
    [Fact]
    public void Read_DocumentExactlyAtSizeCap_IsRead()
    {
        const string Json = """{"schema":1,"blocks":[{"kind":"text","text":"Hi"}]}""";
        Write("views/a.json", Json);
        var cap = Encoding.UTF8.GetByteCount(Json);

        var read = PluginViewDocumentReader.Read(_folder, AdminPageManifest(), cap);

        read.Violation.Should().BeNull();
        read.Views.Find("views/a.json").Should().NotBeNull();
    }

    /// <summary>The members that load or navigate (<c>src</c>, <c>image</c>, <c>to</c>, and <c>href</c> in any case) are checked as links, so an unsafe value breaks ViewInvalid.</summary>
    [Fact]
    public void Read_UrlLikeMemberUnsafe_BreaksViewInvalid()
    {
        foreach (var member in new[] { "src", "image", "to", "HREF", "Url" })
        {
            Write("views/a.json", $$"""{"schema":1,"blocks":[{"kind":"text","text":"x","{{member}}":"javascript:alert(1)"}]}""");
            AssertBreaks(AdminPageManifest(), PluginManifestRule.ViewInvalid);
        }
    }

    /// <summary>A safe host-relative path or https URL in <c>src</c>, <c>image</c> and <c>to</c> is accepted.</summary>
    [Fact]
    public void Read_UrlLikeMemberSafe_IsAccepted()
    {
        Write("views/a.json", """{"schema":1,"blocks":[{"kind":"text","text":"x","src":"/img/a.png","image":"https://cdn.example.com/a.png","to":"/services"}]}""");

        var read = PluginViewDocumentReader.Read(_folder, AdminPageManifest(), MaxViewBytes);

        read.Violation.Should().BeNull();
    }

    /// <summary><c>target</c> and <c>formAction</c>, in any case and at any depth, are refused outright with ViewInvalid.</summary>
    [Fact]
    public void Read_TargetOrFormAction_BreaksViewInvalid()
    {
        foreach (var member in new[] { "\"target\":\"_blank\"", "\"formAction\":\"/docs\"", "\"formaction\":\"/docs\"", "\"Target\":\"_self\"" })
        {
            Write("views/a.json", $$"""{"schema":1,"blocks":[{"kind":"link","label":"Go","href":"/docs",{{member}}}]}""");
            AssertBreaks(AdminPageManifest(), PluginManifestRule.ViewInvalid);

            Write("views/a.json", $$"""{"schema":1,"blocks":[{"kind":"tabs","tabs":[{"title":"T","blocks":[{"kind":"table","rows":[{{{member}}}]}]}]}]}""");
            AssertBreaks(AdminPageManifest(), PluginManifestRule.ViewInvalid);
        }
    }

    /// <summary>An icon that is not a plain lowercase name of 1 to 64 letters, digits and hyphens (a URL, upper case, a space, empty, too long, a newline, not a string) breaks ViewInvalid.</summary>
    [Fact]
    public void Read_IconNotPlainName_BreaksViewInvalid()
    {
        foreach (var icon in new[]
        {
            "\"https://evil.example/i.svg\"",
            "\"/img/i.svg\"",
            "\"Chart\"",
            "\"chart bar\"",
            "\"\"",
            $"\"{new string('a', MaxIconNameLength + 1)}\"",
            "\"chart\\n\"",
            "1",
        })
        {
            Write("views/a.json", $$"""{"schema":1,"blocks":[{"kind":"header","title":"T","icon":{{icon}}}]}""");
            AssertBreaks(AdminPageManifest(), PluginManifestRule.ViewInvalid);
        }
    }

    /// <summary>A plain icon name, up to the longest allowed, is accepted.</summary>
    [Fact]
    public void Read_IconPlainName_IsAccepted()
    {
        foreach (var icon in new[] { "chart-bar", "a", new string('a', MaxIconNameLength) })
        {
            Write("views/a.json", $$"""{"schema":1,"blocks":[{"kind":"header","title":"T","icon":"{{icon}}"}]}""");

            var read = PluginViewDocumentReader.Read(_folder, AdminPageManifest(), MaxViewBytes);

            read.Violation.Should().BeNull(icon);
        }
    }

    /// <summary>A host-relative link that reaches the host API, in any case, after dot segments or behind a percent escape, breaks ViewInvalid.</summary>
    [Fact]
    public void Read_LinkToHostApi_BreaksViewInvalid()
    {
        foreach (var href in new[] { "/api/admin/plugins", "/API/admin", "/api", "/api?x=1", "/api#a", "/docs/../api/x", "/./api/x", "/%61pi/x", "/%2e%2e/api/x" })
        {
            Write("views/a.json", $$"""{"schema":1,"blocks":[{"kind":"link","label":"Go","href":"{{href}}"}]}""");
            AssertBreaks(AdminPageManifest(), PluginManifestRule.ViewInvalid);
        }
    }

    /// <summary>A host-relative link that only resembles the API path (<c>/apis</c>, <c>/docs/api</c>) is accepted.</summary>
    [Fact]
    public void Read_LinkResemblingApi_IsAccepted()
    {
        foreach (var href in new[] { "/apis/x", "/docs/api/x", "/apix" })
        {
            Write("views/a.json", $$"""{"schema":1,"blocks":[{"kind":"link","label":"Go","href":"{{href}}"}]}""");

            var read = PluginViewDocumentReader.Read(_folder, AdminPageManifest(), MaxViewBytes);

            read.Violation.Should().BeNull(href);
        }
    }

    /// <summary>A member name used twice in one object, at the root, in a block or deep in a table, breaks ViewInvalid instead of failing when the page is served.</summary>
    [Fact]
    public void Read_DuplicateMemberName_BreaksViewInvalid()
    {
        foreach (var json in new[]
        {
            """{"schema":1,"schema":1,"blocks":[]}""",
            """{"schema":1,"blocks":[{"kind":"text","kind":"text","text":"x"}]}""",
            """{"schema":1,"blocks":[{"kind":"table","rows":[{"a":1,"a":2}]}]}""",
            """{"schema":1,"blocks":[{"kind":"text","text":"x","t\u0065xt":"y"}]}""",
        })
        {
            Write("views/a.json", json);
            AssertBreaks(AdminPageManifest(), PluginManifestRule.ViewInvalid);
        }
    }

    /// <summary>Names that differ only in case are different members and are accepted.</summary>
    [Fact]
    public void Read_MemberNamesDifferingInCase_AreAccepted()
    {
        Write("views/a.json", """{"schema":1,"blocks":[{"kind":"table","rows":[{"a":1,"A":2}]}]}""");

        var read = PluginViewDocumentReader.Read(_folder, AdminPageManifest(), MaxViewBytes);

        read.Violation.Should().BeNull();
    }

    /// <summary>A document nested exactly to the depth bound is accepted.</summary>
    [Fact]
    public void Read_DocumentAtMaxDepth_IsAccepted()
    {
        Write("views/a.json", NestedDocument(MaxJsonDepth));

        var read = PluginViewDocumentReader.Read(_folder, AdminPageManifest(), MaxViewBytes);

        read.Violation.Should().BeNull();
    }

    /// <summary>A document nested one level past the depth bound breaks ViewInvalid.</summary>
    [Fact]
    public void Read_DocumentPastMaxDepth_BreaksViewInvalid()
    {
        Write("views/a.json", NestedDocument(MaxJsonDepth + 1));

        AssertBreaks(AdminPageManifest(), PluginManifestRule.ViewInvalid);
    }

    /// <summary>A view file that is a symbolic link to a file outside the package breaks ViewInvalid, and the detail names no host path.</summary>
    [Fact]
    public void Read_ViewFileIsLink_BreaksViewInvalid()
    {
        var outside = Path.Combine(Path.GetTempPath(), $"view-rules-target-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outside);
        try
        {
            var target = Path.Combine(outside, "a.json");
            File.WriteAllText(target, """{"schema":1,"blocks":[]}""");
            Directory.CreateDirectory(Path.Combine(_folder, "views"));
            if (!PluginLinkTestSeeds.TryCreateFileLink(Path.Combine(_folder, "views", "a.json"), target))
            {
                return;
            }

            AssertBreaks(AdminPageManifest(), PluginManifestRule.ViewInvalid);
            AssertDetailNamesNoHostPath(outside);
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    /// <summary>A view reached through a folder that is a symbolic link (a junction on Windows) breaks ViewInvalid, and the detail names no host path.</summary>
    [Fact]
    public void Read_ViewFolderIsLink_BreaksViewInvalid()
    {
        var outside = Path.Combine(Path.GetTempPath(), $"view-rules-target-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outside);
        try
        {
            File.WriteAllText(Path.Combine(outside, "a.json"), """{"schema":1,"blocks":[]}""");
            PluginLinkTestSeeds.CreateDirectoryLink(Path.Combine(_folder, "views"), outside);

            AssertBreaks(AdminPageManifest(), PluginManifestRule.ViewInvalid);
            AssertDetailNamesNoHostPath(outside);
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    /// <summary>Text that is not JSON is refused with a detail naming the package-relative path and the exception type, never the host path or the parser message.</summary>
    [Fact]
    public void Read_InvalidJson_DetailNamesPackagePathAndExceptionTypeOnly()
    {
        Write("views/a.json", "{\"schema\":1,\"blocks\":[secret-text]}");

        var read = PluginViewDocumentReader.Read(_folder, AdminPageManifest(), MaxViewBytes);

        read.Violation.Should().NotBeNull();
        read.Violation!.Rule.Should().Be(PluginManifestRule.ViewInvalid);
        read.Violation.Detail.Should().Contain("views/a.json").And.Contain("Exception");
        read.Violation.Detail.Should().NotContain(_folder).And.NotContain("secret").And.NotContain("LineNumber");
    }

    /// <summary>
    /// A document another process holds exclusively cannot be opened; the I/O refusal names the package-relative path and
    /// the exception type only, never the absolute host path the exception message carries.
    /// </summary>
    [Fact]
    public void Read_UnreadableDocument_DetailNamesPackagePathAndExceptionTypeOnly()
    {
        Write("views/a.json", "{\"schema\":1,\"blocks\":[]}");
        using var held = new FileStream(
            Path.Combine(_folder, "views", "a.json"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var read = PluginViewDocumentReader.Read(_folder, AdminPageManifest(), MaxViewBytes);

        read.Violation.Should().NotBeNull();
        read.Violation!.Rule.Should().Be(PluginManifestRule.ViewMissing);
        read.Violation.Detail.Should().Contain("views/a.json").And.Contain("IOException");
        read.Violation.Detail.Should().NotContain(_folder);
    }

    /// <summary>A manifest whose admin page shows <c>views/a.json</c> and whose admin api declares GET /site and POST /publish.</summary>
    /// <returns>The manifest.</returns>
    private static PluginManifest StateManifest()
    {
        return Manifest(""","api":{"admin":[{"method":"get","path":"/site"},{"method":"post","path":"/publish"}]},"ui":{"admin":{"pages":[{"id":"overview","path":"/o","title":"O","view":"views/a.json"}]}}""");
    }

    /// <summary>A visibility condition, a form reset and follow-ups on an action and a form submit are accepted, and a condition value is not read as a label key.</summary>
    [Fact]
    public void Read_ViewStateMembers_AreAccepted()
    {
        Write("views/a.json", """
            {"schema":1,"blocks":[
              {"kind":"stat","id":"status","label":"Status","source":{"route":{"method":"get","path":"/site"},"field":"site.status"}},
              {"kind":"form","fields":[],"visibleWhen":{"route":{"method":"get","path":"/site"},"field":"site","equals":null},
               "submit":{"route":{"method":"post","path":"/publish"},"label":"Create","resetOnSuccess":true,
                 "followUp":{"refresh":["status"],"until":{"route":{"method":"get","path":"/site"},"field":"site.status","equals":"ready"}}}},
              {"kind":"action","label":"Publish","route":{"method":"post","path":"/publish"},
               "followUp":{"refresh":["status"],"until":{"route":{"method":"get","path":"/site"},"field":"site.status","equals":["live","failed.key",3,true]}}}
            ]}
            """);

        var read = PluginViewDocumentReader.Read(_folder, StateManifest(), MaxViewBytes);

        read.Violation.Should().BeNull();
    }

    /// <summary>A condition that is not exactly a GET route, a dotted field and a value or bounded list of scalars breaks ViewConditionInvalid.</summary>
    /// <param name="condition">The condition JSON.</param>
    [Theory]
    [InlineData("""{"route":{"method":"post","path":"/publish"},"field":"a","equals":"x"}""")]
    [InlineData("""{"field":"a","equals":"x"}""")]
    [InlineData("""{"route":{"method":"get","path":"/site"},"equals":"x"}""")]
    [InlineData("""{"route":{"method":"get","path":"/site"},"field":"a"}""")]
    [InlineData("""{"route":{"method":"get","path":"/site"},"field":"a..b","equals":"x"}""")]
    [InlineData("""{"route":{"method":"get","path":"/site"},"field":"a","equals":[]}""")]
    [InlineData("""{"route":{"method":"get","path":"/site"},"field":"a","equals":{"v":1}}""")]
    [InlineData("""{"route":{"method":"get","path":"/site"},"field":"a","equals":[["x"]]}""")]
    [InlineData("""{"route":{"method":"get","path":"/site"},"field":"a","equals":"x","extra":1}""")]
    [InlineData("\"ready\"")]
    public void Read_ConditionMalformed_BreaksViewConditionInvalid(string condition)
    {
        Write("views/a.json", $$"""{"schema":1,"blocks":[{"kind":"text","text":"Hi","visibleWhen":{{condition}}}]}""");

        AssertBreaks(StateManifest(), PluginManifestRule.ViewConditionInvalid);
    }

    /// <summary>A condition listing more values than the SDK bound breaks ViewConditionInvalid.</summary>
    [Fact]
    public void Read_ConditionOverValueBound_BreaksViewConditionInvalid()
    {
        var values = string.Join(",", Enumerable.Range(0, PluginViews.MaxConditionValues + 1));
        Write("views/a.json", $$$"""{"schema":1,"blocks":[{"kind":"text","text":"Hi","visibleWhen":{"route":{"method":"get","path":"/site"},"field":"a","equals":[{{{values}}}]}}]}""");

        AssertBreaks(StateManifest(), PluginManifestRule.ViewConditionInvalid);
    }

    /// <summary>A visibility condition on an object that is not a block breaks ViewConditionInvalid.</summary>
    [Fact]
    public void Read_VisibleWhenOutsideBlock_BreaksViewConditionInvalid()
    {
        Write("views/a.json", """{"schema":1,"blocks":[{"kind":"tabs","tabs":[{"id":"t","title":"T","visibleWhen":{"route":{"method":"get","path":"/site"},"field":"a","equals":"x"},"blocks":[]}]}]}""");

        AssertBreaks(StateManifest(), PluginManifestRule.ViewConditionInvalid);
    }

    /// <summary>A condition route the area does not declare breaks ViewRouteUndeclared, like every other route reference.</summary>
    [Fact]
    public void Read_ConditionRouteUndeclared_BreaksViewRouteUndeclared()
    {
        Write("views/a.json", """{"schema":1,"blocks":[{"kind":"text","text":"Hi","visibleWhen":{"route":{"method":"get","path":"/other"},"field":"a","equals":"x"}}]}""");

        AssertBreaks(StateManifest(), PluginManifestRule.ViewRouteUndeclared);
    }

    /// <summary>A follow-up that is malformed, misplaced or refreshes an unknown block, and a reset flag that is not a boolean in a submit, break ViewFollowUpInvalid.</summary>
    /// <param name="block">The block JSON placed beside a stat block whose id is <c>status</c>.</param>
    [Theory]
    [InlineData("""{"kind":"action","label":"P","route":{"method":"post","path":"/publish"},"followUp":{"refresh":["nope"],"until":{"route":{"method":"get","path":"/site"},"field":"a","equals":"x"}}}""")]
    [InlineData("""{"kind":"action","label":"P","route":{"method":"post","path":"/publish"},"followUp":{"refresh":[],"until":{"route":{"method":"get","path":"/site"},"field":"a","equals":"x"}}}""")]
    [InlineData("""{"kind":"action","label":"P","route":{"method":"post","path":"/publish"},"followUp":{"refresh":["status"]}}""")]
    [InlineData("""{"kind":"action","label":"P","route":{"method":"post","path":"/publish"},"followUp":{"refresh":[""],"until":{"route":{"method":"get","path":"/site"},"field":"a","equals":"x"}}}""")]
    [InlineData("""{"kind":"text","text":"Hi","followUp":{"refresh":["status"],"until":{"route":{"method":"get","path":"/site"},"field":"a","equals":"x"}}}""")]
    [InlineData("""{"kind":"form","fields":[],"resetOnSuccess":true,"submit":{"route":{"method":"post","path":"/publish"},"label":"S"}}""")]
    [InlineData("""{"kind":"form","fields":[],"submit":{"route":{"method":"post","path":"/publish"},"label":"S","resetOnSuccess":"yes"}}""")]
    public void Read_FollowUpOrResetMalformed_BreaksViewFollowUpInvalid(string block)
    {
        Write("views/a.json", $$"""{"schema":1,"blocks":[{"kind":"stat","id":"status","label":"S","value":"x"},{{block}}]}""");

        AssertBreaks(StateManifest(), PluginManifestRule.ViewFollowUpInvalid);
    }

    /// <summary>An action whose body and label are read from one declared GET route is accepted.</summary>
    [Fact]
    public void Read_ActionBodySource_IsAccepted()
    {
        Write("views/a.json", """{"schema":1,"blocks":[{"kind":"action","id":"p","label":"P","route":{"method":"post","path":"/publish"},"labelSource":{"route":{"method":"get","path":"/site"},"field":"l"},"bodySource":{"route":{"method":"get","path":"/site"},"fields":{"expectedVersion":"a.b"}}}]}""");

        var read = PluginViewDocumentReader.Read(_folder, ClientActionManifest(), MaxViewBytes);

        read.Violation.Should().BeNull();
    }

    /// <summary>A body source of a wrong shape or on a wrong block breaks ViewActionBodyInvalid; nothing guesses what to send.</summary>
    /// <param name="block">The block JSON.</param>
    [Theory]
    [InlineData("""{"kind":"text","text":"Hi","bodySource":{"route":{"method":"get","path":"/site"},"fields":{"a":"b"}}}""")]
    [InlineData("""{"kind":"action","label":"P","route":{"method":"post","path":"/publish"},"bodySource":{"route":{"method":"get","path":"/site"},"fields":{"a":"b"}}}""")]
    [InlineData("""{"kind":"action","id":"p","label":"P","route":{"method":"get","path":"/site"},"bodySource":{"route":{"method":"get","path":"/site"},"fields":{"a":"b"}}}""")]
    [InlineData("""{"kind":"action","id":"p","label":"P","route":{"method":"post","path":"/publish"},"bodySource":{"route":{"method":"post","path":"/publish"},"fields":{"a":"b"}}}""")]
    [InlineData("""{"kind":"action","id":"p","label":"P","route":{"method":"post","path":"/publish"},"bodySource":{"route":{"method":"get","path":"/site"}}}""")]
    [InlineData("""{"kind":"action","id":"p","label":"P","route":{"method":"post","path":"/publish"},"bodySource":{"route":{"method":"get","path":"/site"},"fields":{}}}""")]
    [InlineData("""{"kind":"action","id":"p","label":"P","route":{"method":"post","path":"/publish"},"bodySource":{"route":{"method":"get","path":"/site"},"fields":{"__proto__":"b"}}}""")]
    [InlineData("""{"kind":"action","id":"p","label":"P","route":{"method":"post","path":"/publish"},"bodySource":{"route":{"method":"get","path":"/site"},"fields":{"label":"b"}}}""")]
    [InlineData("""{"kind":"action","id":"p","label":"P","route":{"method":"post","path":"/publish"},"bodySource":{"route":{"method":"get","path":"/site"},"fields":{"a":"b c"}}}""")]
    [InlineData("""{"kind":"action","id":"p","label":"P","route":{"method":"post","path":"/publish"},"bodySource":{"route":{"method":"get","path":"/site"},"fields":{"a":1}}}""")]
    [InlineData("""{"kind":"action","id":"p","label":"P","route":{"method":"post","path":"/publish"},"bodySource":{"route":{"method":"get","path":"/site"},"fields":{"a":"b"},"extra":1}}""")]
    [InlineData("""{"kind":"action","id":"p","label":"P","route":{"method":"post","path":"/publish"},"labelSource":{"route":{"method":"get","path":"/publish"},"field":"l"},"bodySource":{"route":{"method":"get","path":"/site"},"fields":{"a":"b"}}}""")]
    public void Read_ActionBodySourceMalformed_BreaksViewActionBodyInvalid(string block)
    {
        Write("views/a.json", $$"""{"schema":1,"blocks":[{{block}}]}""");

        AssertBreaks(ClientActionManifest(), PluginManifestRule.ViewActionBodyInvalid);
    }

    /// <summary>A body source naming more members than a page may send breaks ViewActionBodyInvalid.</summary>
    [Fact]
    public void Read_ActionBodySourceTooManyFields_BreaksViewActionBodyInvalid()
    {
        var fields = string.Join(",", Enumerable.Range(0, 11).Select(index => $"\"a{index}\":\"b\""));
        Write("views/a.json", """{"schema":1,"blocks":[{"kind":"action","id":"p","label":"P","route":{"method":"post","path":"/publish"},"bodySource":{"route":{"method":"get","path":"/site"},"fields":{""" + fields + """}}}]}""");

        AssertBreaks(ClientActionManifest(), PluginManifestRule.ViewActionBodyInvalid);
    }

    /// <summary>A body source reading a route the area does not declare breaks ViewRouteUndeclared, like every other reference.</summary>
    [Fact]
    public void Read_ActionBodySourceRouteUndeclared_BreaksViewRouteUndeclared()
    {
        Write("views/a.json", """{"schema":1,"blocks":[{"kind":"action","id":"p","label":"P","route":{"method":"post","path":"/publish"},"bodySource":{"route":{"method":"get","path":"/other"},"fields":{"a":"b"}}}]}""");

        AssertBreaks(ClientActionManifest(), PluginManifestRule.ViewRouteUndeclared);
    }

    /// <summary>A body source on an admin page breaks ViewActionBodyInvalid: the admin panel would post without it.</summary>
    [Fact]
    public void Read_ActionBodySourceOnAdminPage_BreaksViewActionBodyInvalid()
    {
        Write("views/a.json", """{"schema":1,"blocks":[{"kind":"action","id":"p","label":"P","route":{"method":"post","path":"/publish"},"bodySource":{"route":{"method":"get","path":"/site"},"fields":{"a":"b"}}}]}""");

        AssertBreaks(StateManifest(), PluginManifestRule.ViewActionBodyInvalid);
    }

    /// <summary>A manifest whose client page shows <c>views/a.json</c> and whose client api declares GET /site and POST /publish.</summary>
    /// <returns>The manifest.</returns>
    private static PluginManifest ClientActionManifest()
    {
        return Manifest(""","api":{"client":[{"method":"get","path":"/site","permission":"Any"},{"method":"post","path":"/publish","permission":"Any"}]},"ui":{"client":{"pages":[{"id":"overview","path":"/o","title":"O","permission":"Any","view":"views/a.json"}]}}""");
    }

    /// <summary>Builds a valid document whose nesting reaches the given depth, the root counted as one level.</summary>
    /// <param name="depth">The total nesting depth, at least <see cref="DepthBeforePadding"/>.</param>
    /// <returns>The document text.</returns>
    private static string NestedDocument(int depth)
    {
        var padding = depth - DepthBeforePadding;
        var open = string.Concat(Enumerable.Repeat("[", padding));
        var close = string.Concat(Enumerable.Repeat("]", padding));
        return $$"""{"schema":1,"blocks":[{"kind":"text","text":"x","data":{{open}}{{close}}}]}""";
    }

    /// <summary>A manifest whose client page shows <c>views/a.json</c> and whose client api declares GET /site and POST /publish.</summary>
    /// <returns>The manifest.</returns>
    private static PluginManifest ClientPageManifest()
    {
        return Manifest(""","api":{"client":[{"method":"get","path":"/site","permission":"Any"},{"method":"post","path":"/publish","permission":"Any"}]},"ui":{"client":{"pages":[{"id":"overview","path":"/o","title":"O","view":"views/a.json","permission":"Any"}]}}""");
    }

    /// <summary>A split holding a chat beside a preview with two sources, on a client page, is accepted.</summary>
    [Fact]
    public void Read_SplitWithPreviewOnClientPage_IsAccepted()
    {
        Write("views/a.json", """
            {"schema":1,"blocks":[{"kind":"split","panes":[
              {"blocks":[{"kind":"chat","send":{"method":"post","path":"/publish"}}]},
              {"blocks":[{"kind":"preview","size":"phone","sources":[
                {"id":"preview","label":"Preview","route":{"method":"get","path":"/site"},"field":"urls.preview"},
                {"id":"live","label":"Live","route":{"method":"get","path":"/site"},"field":"urls.live"}]}]}
            ]}]}
            """);

        PluginViewDocumentReader.Read(_folder, ClientPageManifest(), MaxViewBytes).Violation.Should().BeNull();
    }

    /// <summary>A split without exactly two panes of blocks, or inside another split at any depth, breaks ViewSplitInvalid.</summary>
    /// <param name="block">The block under test.</param>
    [Theory]
    [InlineData("""{"kind":"split"}""")]
    [InlineData("""{"kind":"split","panes":{}}""")]
    [InlineData("""{"kind":"split","panes":[{"blocks":[]}]}""")]
    [InlineData("""{"kind":"split","panes":[{"blocks":[]},{"blocks":[]},{"blocks":[]}]}""")]
    [InlineData("""{"kind":"split","panes":[{"blocks":[]},{}]}""")]
    [InlineData("""{"kind":"split","panes":[{"blocks":[]},{"blocks":[],"extra":1}]}""")]
    [InlineData("""{"kind":"split","panes":[{"blocks":[]},{"blocks":[{"nokind":1}]}]}""")]
    [InlineData("""{"kind":"split","panes":[{"blocks":[]},{"blocks":[{"kind":"split","panes":[{"blocks":[]},{"blocks":[]}]}]}]}""")]
    [InlineData("""{"kind":"split","panes":[{"blocks":[]},{"blocks":[{"kind":"tabs","tabs":[{"blocks":[{"kind":"split","panes":[{"blocks":[]},{"blocks":[]}]}]}]}]}]}""")]
    public void Read_SplitMalformed_BreaksViewSplitInvalid(string block)
    {
        Write("views/a.json", $$"""{"schema":1,"blocks":[{{block}}]}""");

        AssertBreaks(ClientPageManifest(), PluginManifestRule.ViewSplitInvalid);
    }

    /// <summary>A preview with bad sources or size breaks ViewPreviewInvalid: the frame must never be guessed at.</summary>
    /// <param name="block">The block under test.</param>
    [Theory]
    [InlineData("""{"kind":"preview"}""")]
    [InlineData("""{"kind":"preview","sources":[]}""")]
    [InlineData("""{"kind":"preview","sources":{}}""")]
    [InlineData("""{"kind":"preview","size":"tablet","sources":[{"id":"a","label":"A","route":{"method":"get","path":"/site"},"field":"url"}]}""")]
    [InlineData("""{"kind":"preview","size":1,"sources":[{"id":"a","label":"A","route":{"method":"get","path":"/site"},"field":"url"}]}""")]
    [InlineData("""{"kind":"preview","sources":[{"id":"A b","label":"A","route":{"method":"get","path":"/site"},"field":"url"}]}""")]
    [InlineData("""{"kind":"preview","sources":[{"label":"A","route":{"method":"get","path":"/site"},"field":"url"}]}""")]
    [InlineData("""{"kind":"preview","sources":[{"id":"a","label":"","route":{"method":"get","path":"/site"},"field":"url"}]}""")]
    [InlineData("""{"kind":"preview","sources":[{"id":"a","label":"A","route":{"method":"post","path":"/publish"},"field":"url"}]}""")]
    [InlineData("""{"kind":"preview","sources":[{"id":"a","label":"A","route":{"method":"get","path":"/site"},"field":"a..b"}]}""")]
    [InlineData("""{"kind":"preview","sources":[{"id":"a","label":"A","route":{"method":"get","path":"/site"},"field":"url","address":"https://evil.example"}]}""")]
    [InlineData("""{"kind":"preview","sources":[{"id":"a","label":"A","route":{"method":"get","path":"/site"},"field":"u"},{"id":"a","label":"B","route":{"method":"get","path":"/site"},"field":"v"}]}""")]
    public void Read_PreviewMalformed_BreaksViewPreviewInvalid(string block)
    {
        Write("views/a.json", $$"""{"schema":1,"blocks":[{{block}}]}""");

        AssertBreaks(ClientPageManifest(), PluginManifestRule.ViewPreviewInvalid);
    }

    /// <summary>More sources than the SDK bound break ViewPreviewInvalid.</summary>
    [Fact]
    public void Read_PreviewOverSourceBound_BreaksViewPreviewInvalid()
    {
        var sources = string.Join(',', Enumerable.Range(0, PluginViews.MaxPreviewSources + 1).Select(i =>
            $$"""{"id":"s{{i}}","label":"S","route":{"method":"get","path":"/site"},"field":"url"}"""));
        Write("views/a.json", $$"""{"schema":1,"blocks":[{"kind":"preview","sources":[{{sources}}]}]}""");

        AssertBreaks(ClientPageManifest(), PluginManifestRule.ViewPreviewInvalid);
    }

    /// <summary>A preview on an admin page breaks ViewPreviewInvalid: no document there gets the narrowed frame policy.</summary>
    [Fact]
    public void Read_PreviewOnAdminPage_BreaksViewPreviewInvalid()
    {
        Write("views/a.json", ValidPreviewDocument);

        AssertBreaks(StateManifest(), PluginManifestRule.ViewPreviewInvalid);
    }

    /// <summary>A preview in a client slot breaks ViewPreviewInvalid: a slot lives in a document with the wide frame policy.</summary>
    [Fact]
    public void Read_PreviewInSlot_BreaksViewPreviewInvalid()
    {
        Write("views/a.json", ValidPreviewDocument);

        AssertBreaks(
            Manifest(""","api":{"client":[{"method":"get","path":"/site","permission":"Any"}]},"ui":{"slots":[{"id":"w","slot":"client.dashboard.cards","title":"T","view":"views/a.json","permission":"Any"}]}"""),
            PluginManifestRule.ViewPreviewInvalid);
    }

    /// <summary>A preview source naming a route the area does not declare breaks ViewRouteUndeclared.</summary>
    [Fact]
    public void Read_PreviewRouteUndeclared_BreaksViewRouteUndeclared()
    {
        Write("views/a.json", """{"schema":1,"blocks":[{"kind":"preview","sources":[{"id":"a","label":"A","route":{"method":"get","path":"/other"},"field":"url"}]}]}""");

        AssertBreaks(ClientPageManifest(), PluginManifestRule.ViewRouteUndeclared);
    }

    /// <summary>A preview selection in its exact shape, with a chat taking it, is accepted.</summary>
    [Fact]
    public void Read_ValidSelection_IsAccepted()
    {
        Write("views/a.json", SelectionDocument(SelectionMember("""["a"]""", "post"), chatSelection: "true"));

        PluginViewDocumentReader.Read(_folder, ClientPageManifest(), MaxViewBytes).Violation.Should().BeNull();
    }

    /// <summary>Every broken shape of a selection is refused with its own rule: an unknown source, none, a repeat, a GET lookup, an extra member, a chat value other than true, a chat with no preview selection.</summary>
    /// <param name="selection">The preview's selection member, or empty for none.</param>
    /// <param name="chat">The chat's selection value.</param>
    [Theory]
    [InlineData("""["nope"]|post""", "true")]
    [InlineData("""[]|post""", "true")]
    [InlineData("""["a","a"]|post""", "true")]
    [InlineData("""["a"]|get""", "true")]
    [InlineData("""["a"]|post""", "1")]
    [InlineData("", "true")]
    public void Read_BrokenSelection_BreaksViewSelectionInvalid(string selection, string chat)
    {
        var member = selection.Length == 0 ? string.Empty : SelectionMember(selection.Split('|')[0], selection.Split('|')[1]);
        Write("views/a.json", SelectionDocument(member, chat));

        AssertBreaks(ClientPageManifest(), PluginManifestRule.ViewSelectionInvalid);
    }

    /// <summary>A source id that is no string, with a selection on the preview, is a refusal and not an exception, at install and at load alike.</summary>
    /// <param name="id">The source's id, as JSON.</param>
    [Theory]
    [InlineData("1")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("true")]
    public void Read_SelectionWithSourceIdNotAString_BreaksPreviewRule(string id)
    {
        Write("views/a.json", $$$"""{"schema":1,"blocks":[{"kind":"preview","sources":[{"id":{{{id}}},"label":"A","route":{"method":"get","path":"/site"},"field":"url"}]{{{SelectionMember("""["a"]""", "post")}}}}]}""");

        AssertBreaks(ClientPageManifest(), PluginManifestRule.ViewPreviewInvalid);
    }

    /// <summary>An extra member in a selection, which the panel would otherwise ignore, is refused.</summary>
    [Fact]
    public void Read_SelectionExtraMember_BreaksViewSelectionInvalid()
    {
        var member = SelectionMember("""["a"]""", "post").Replace("\"sources\"", "\"extra\":1,\"sources\"", StringComparison.Ordinal);
        Write("views/a.json", SelectionDocument(member, "true"));

        AssertBreaks(ClientPageManifest(), PluginManifestRule.ViewSelectionInvalid);
    }

    /// <summary>The selection member of a preview.</summary>
    /// <param name="sources">The sources, as JSON.</param>
    /// <param name="method">The lookup route's method.</param>
    /// <returns>The member, with a leading comma.</returns>
    private static string SelectionMember(string sources, string method) =>
        $$$""","selection":{"sources":{{{sources}}},"route":{"method":"{{{method}}}","path":"/publish"},"release":{"route":{"method":"get","path":"/site"},"field":"release"}}""";

    /// <summary>A document with a preview and a chat taking a selection.</summary>
    /// <param name="member">The preview's selection member.</param>
    /// <param name="chatSelection">The chat's selection value.</param>
    /// <returns>The document.</returns>
    private static string SelectionDocument(string member, string chatSelection) =>
        $$$"""{"schema":1,"blocks":[{"kind":"chat","selection":{{{chatSelection}}},"send":{"method":"post","path":"/publish"}},{"kind":"preview","sources":[{"id":"a","label":"A","route":{"method":"get","path":"/site"},"field":"url"}]{{{member}}}}]}""";

    /// <summary>Asserts the violation of reading the admin page manifest names neither the scratch folder nor another host folder.</summary>
    /// <param name="other">A host folder outside the package the detail must not name either.</param>
    private void AssertDetailNamesNoHostPath(string other)
    {
        var read = PluginViewDocumentReader.Read(_folder, AdminPageManifest(), MaxViewBytes);
        read.Violation!.Detail.Should().Contain("views/a.json");
        read.Violation.Detail.Should().NotContain(_folder).And.NotContain(other);
    }
}
