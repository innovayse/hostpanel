using System.Text.Json;
using System.Text.RegularExpressions;
using Hostpanel.Sdk.Packaging.Signing;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Slots;
using Hostpanel.Sdk.Plugins.Ui;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Plugins.Common;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Reads the view documents the pages and slots of a manifest name and checks them against the view schema
/// (<see cref="PluginViews"/>), once, for both the startup load and the install. A document is kept as a cloned
/// <see cref="JsonElement"/> so the loaded plugin serves it without touching the disk again. Text keys are checked by
/// <see cref="PluginManifestValidator"/>, the same code that checks manifest labels.
/// </summary>
/// <remarks>
/// Every refusal detail names the package-relative path and, for a fault, the exception type only: an absolute host
/// path or an exception message never reaches a log line or an install response.
/// </remarks>
internal static partial class PluginViewDocumentReader
{
    /// <summary>The member of the root that holds the schema version.</summary>
    private const string SchemaMember = "schema";

    /// <summary>The <c>ui</c> member, and the log name, of the client area's pages.</summary>
    private const string ClientAreaMember = "client";

    /// <summary>The <c>ui</c> member, and the log name, of the admin area's pages.</summary>
    private const string AdminAreaMember = "admin";

    /// <summary>
    /// How many bytes are read from a view document at a time. The read stops at the first chunk that passes the cap,
    /// so a document is never read more than the cap plus one byte, even when it grew after its length was checked.
    /// </summary>
    private const int ReadChunkBytes = 8192;

    /// <summary>
    /// The deepest nesting of objects and arrays a view document may have, the root counted as one level. A view
    /// needs a handful of levels (root, blocks, block, tabs, tab, blocks, ...); the bound keeps the parser and the
    /// recursive walk of a hostile document short.
    /// </summary>
    private const int MaxJsonDepth = 32;

    /// <summary>The member of a block that names its kind.</summary>
    private const string KindMember = "kind";

    /// <summary>The member of the root, and of a tab, that holds an array of blocks.</summary>
    private const string BlocksMember = "blocks";

    /// <summary>The member of a <c>tabs</c> block that holds its tabs.</summary>
    private const string TabsMember = "tabs";

    /// <summary>The member of a block (a header) that holds an array of action blocks.</summary>
    private const string ActionsMember = "actions";

    /// <summary>The member of a block that holds its id, which a refresh names.</summary>
    private const string IdMember = "id";

    /// <summary>The member of a form block that holds what submitting does.</summary>
    private const string SubmitMember = "submit";

    /// <summary>The block kind that may carry a <c>followUp</c> of its own.</summary>
    private const string ActionKind = "action";

    /// <summary>The member of a preview and of a chat declaring element selection; the host's own contract name until the SDK documents it.</summary>
    private const string SelectionMember = "selection";

    /// <summary>The member of a preview's selection listing the sources that may select.</summary>
    private const string SelectionSourcesMember = "sources";

    /// <summary>The member of a preview's selection saying which release the preview holds.</summary>
    private const string SelectionReleaseMember = "release";

    /// <summary>The members of a preview's selection: sources, route and release.</summary>
    private const int SelectionMemberCount = 3;

    /// <summary>The members of a selection's release: route and field.</summary>
    private const int SelectionReleaseMemberCount = 2;

    /// <summary>The block kind of a chat.</summary>
    private const string ChatKind = "chat";

    /// <summary>The member of an action block that builds the body of its call from what the page has read.</summary>
    private const string BodySourceMember = "bodySource";

    /// <summary>The member of a body source mapping each body member to a dotted path into the reply.</summary>
    private const string BodySourceFieldsMember = "fields";

    /// <summary>The number of members a body source holds: route and fields, nothing else.</summary>
    private const int BodySourceMemberCount = 2;

    /// <summary>The most members a body source may send; an expected version needs one, and a page is no form.</summary>
    private const int MaxBodySourceFields = 10;

    /// <summary>The block kind of a header.</summary>
    private const string HeaderKind = "header";

    /// <summary>The block kind of a table.</summary>
    private const string TableKind = "table";

    /// <summary>The member of a header that makes it one slim line, a boolean.</summary>
    private const string CompactMember = "compact";

    /// <summary>The member of a header that reads its title from a route.</summary>
    private const string TitleSourceMember = "titleSource";

    /// <summary>The member of a header that reads its smaller line from a route.</summary>
    private const string SubtitleSourceMember = "subtitleSource";

    /// <summary>The member of a table that says how its rows are drawn.</summary>
    private const string DisplayMember = "display";

    /// <summary>The member of a preview that reads the sentence written over its frame.</summary>
    private const string NoteMember = "note";

    /// <summary>
    /// The ways a table may draw its rows; the panel reads anything else as the default, which would hide a typo.
    /// </summary>
    private static readonly string[] TableDisplays = ["table", "cards"];

    /// <summary>The member of an action block naming the route it calls.</summary>
    private const string ActionRouteMember = "route";

    /// <summary>The member of an action block naming where a label that depends on data is read from.</summary>
    private const string LabelSourceMember = "labelSource";

    /// <summary>The member of a condition naming the GET route read.</summary>
    private const string ConditionRouteMember = "route";

    /// <summary>The member of a condition holding the dotted path into the reply.</summary>
    private const string ConditionFieldMember = "field";

    /// <summary>The member of a condition holding the value, or the list of values, the field must equal.</summary>
    private const string ConditionEqualsMember = "equals";

    /// <summary>The number of members a condition holds: route, field and equals, nothing else.</summary>
    private const int ConditionMemberCount = 3;

    /// <summary>The longest dotted field a condition may name, in characters; far above any real reply path.</summary>
    private const int MaxConditionFieldLength = 200;

    /// <summary>The member of a follow-up listing the block ids read again.</summary>
    private const string FollowUpRefreshMember = "refresh";

    /// <summary>The member of a follow-up holding the condition that ends it.</summary>
    private const string FollowUpUntilMember = "until";

    /// <summary>The number of members a follow-up holds: refresh and until, nothing else.</summary>
    private const int FollowUpMemberCount = 2;

    /// <summary>The number of members a pane holds: blocks, nothing else.</summary>
    private const int PaneMemberCount = 1;

    /// <summary>The number of members a preview source holds: id, label, route and field, nothing else.</summary>
    private const int PreviewSourceMemberCount = 4;

    /// <summary>The member of a preview source naming it within its block.</summary>
    private const string PreviewSourceIdMember = "id";

    /// <summary>The member of a preview source holding its label.</summary>
    private const string PreviewSourceLabelMember = "label";

    /// <summary>The member, at any depth, that names an icon; its value must be a plain icon name, never a URL.</summary>
    private const string IconMember = "icon";

    /// <summary>The file extension every view document path ends in.</summary>
    private const string ViewExtension = ".json";

    /// <summary>The separator of a package-relative path.</summary>
    private const char PackagePathSeparator = '/';

    /// <summary>The prefix of a protocol-relative link, which leaves the host although it starts with a slash.</summary>
    private const string ProtocolRelativePrefix = "//";

    /// <summary>The URL scheme an absolute link may use.</summary>
    private const string SafeAbsoluteScheme = "https";

    /// <summary>The host's API path, which a view link may not point the browser at.</summary>
    private const string ApiPath = "/api";

    /// <summary>The prefix of every host API route, which a view link may not point the browser at.</summary>
    private const string ApiPathPrefix = "/api/";

    /// <summary>
    /// A base a host-relative link is resolved against to read the path the browser would request. The
    /// <c>.invalid</c> top-level domain never resolves, so the value is only ever parsed.
    /// </summary>
    private const string LinkResolutionBase = "https://host.invalid/";

    /// <summary>
    /// The members, at any depth, whose value is a link the panel navigates to or loads. The panel renders them as an
    /// <c>href</c> or a <c>src</c>, so a <c>javascript:</c>, <c>data:</c> or protocol-relative value would run or leave
    /// the host; each one must pass <see cref="IsSafeLink"/>. The SDK does not publish this list yet; it should, as a
    /// <c>PluginViews.LinkProperties</c> beside <see cref="PluginViews.LabelProperties"/>, so a plugin author and both
    /// panels read the same one (follow-up for the SDK repository).
    /// </summary>
    private static readonly string[] LinkMembers = ["href", "url", "src", "image", "to"];

    /// <summary>
    /// Members a view may never hold, at any depth: <c>target</c> would let a link open a named window or escape the
    /// panel frame, and <c>formAction</c> would point a form at another address. Neither is a link the host can check,
    /// so they are refused outright rather than passed through.
    /// </summary>
    private static readonly string[] RefusedMembers = ["target", "formAction"];

    /// <summary>The parser options: the documented depth bound; comments and trailing commas stay refused.</summary>
    private static readonly JsonDocumentOptions ParseOptions = new() { MaxDepth = MaxJsonDepth };

    /// <summary>Matches one segment of a package-relative path: lowercase letters, digits, dot, hyphen and underscore, not starting with a dot; nothing after it, not even a newline.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"\A[a-z0-9][a-z0-9_.-]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex PathSegmentPattern();

    /// <summary>Matches a plain icon name: 1 to 64 lowercase letters, digits and hyphens; nothing after it, not even a newline.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"\A[a-z0-9-]{1,64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex IconNamePattern();

    /// <summary>Matches a dotted reply field of a condition: segments of letters, digits, underscore and hyphen joined by single dots; nothing after it.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"\A[A-Za-z0-9_-]+(\.[A-Za-z0-9_-]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex ConditionFieldPattern();

    /// <summary>Matches the id of a preview source; nothing after it, not even a newline.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"\A[a-z0-9][a-z0-9_-]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex PreviewSourceIdPattern();

    /// <summary>A member of a body a view builds: one camelCase JSON name, so no name can reach an object's prototype.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\A[a-z][A-Za-z0-9]{0,63}\z", RegexOptions.CultureInvariant)]
    private static partial Regex BodyMemberPattern();

    /// <summary>Reads and checks every view document the manifest names, under the plugin folder.</summary>
    /// <param name="pluginDirectory">The plugin folder: the folder holding <c>plugin.json</c>.</param>
    /// <param name="manifest">The manifest, already accepted by <see cref="PluginManifestValidator.Validate"/>.</param>
    /// <param name="maxViewBytes">The largest view document accepted, in bytes (<c>Plugins:MaxViewBytes</c>).</param>
    /// <returns>
    /// The documents, the first view rule the plugin breaks, and the faults of the rules added after plugins were
    /// installed (<see cref="PluginViewRead.Advisories"/>): the caller decides whether those refuse the plugin.
    /// </returns>
    internal static PluginViewRead Read(string pluginDirectory, PluginManifest manifest, long maxViewBytes)
    {
        var uses = CollectUses(manifest);
        if (uses.Count == 0)
        {
            return new PluginViewRead(PluginViewDocuments.None, null, []);
        }

        var root = Path.GetFullPath(pluginDirectory);
        var english = PluginManifestValidator.EnglishTexts(manifest);
        var documents = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var advisories = new List<PluginManifestViolation>();

        foreach (var group in uses.GroupBy(use => use.Path, StringComparer.Ordinal))
        {
            var first = group.First();
            var violation = ReadDocument(root, group.Key, first.Where, english, maxViewBytes, out var document, out var scan);
            if (violation is not null)
            {
                return new PluginViewRead(PluginViewDocuments.None, violation, []);
            }

            advisories.AddRange(
                scan.Advisories.Select(a => new PluginManifestViolation(a.Rule, $"{first.Where} {a.Detail}")));

            foreach (var use in group)
            {
                violation = CheckPreviewPlace(use, scan) ?? CheckSelectionPlace(use, scan) ?? CheckActionBodyPlace(use, scan) ?? CheckRoutes(manifest, use, scan);
                if (violation is not null)
                {
                    return new PluginViewRead(PluginViewDocuments.None, violation, []);
                }
            }

            documents[group.Key] = document;
        }

        return new PluginViewRead(new PluginViewDocuments(documents), null, advisories);
    }

    /// <summary>Lists every place a manifest shows a view: client pages, admin pages, then slots.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <returns>The uses in manifest order.</returns>
    private static List<ViewUse> CollectUses(PluginManifest manifest)
    {
        var uses = new List<ViewUse>();
        PageArea[] areas =
            [
                new PageArea(PluginArea.Client, ClientAreaMember, manifest.Ui.Client),
                new PageArea(PluginArea.Admin, AdminAreaMember, manifest.Ui.Admin),
            ];
        foreach (var area in areas)
        {
            foreach (var page in area.Ui.Pages)
            {
                uses.Add(new ViewUse(page.View, area.Area, $"ui.{area.Member}.pages '{page.Id}' view '{page.View}'", IsPage: true));
            }
        }

        foreach (var slot in manifest.Ui.Slots)
        {
            var known = PluginSlots.Find(slot.Slot);
            if (known is not null)
            {
                uses.Add(new ViewUse(slot.View, known.Area, $"ui.slots '{slot.Id}' view '{slot.View}'", IsPage: false));
            }
        }

        return uses;
    }

    /// <summary>Reads one document and applies the rules that need no area: path, file, shape, schema, block kinds and labels.</summary>
    /// <param name="root">The full path of the plugin folder.</param>
    /// <param name="path">The package-relative path.</param>
    /// <param name="where">The first entry naming it, for the log.</param>
    /// <param name="english">The English texts of the manifest.</param>
    /// <param name="maxViewBytes">The largest document accepted, in bytes.</param>
    /// <param name="document">The cloned root element when no rule is broken.</param>
    /// <param name="scan">What the walk found when no rule is broken.</param>
    /// <returns>The violation, or <see langword="null"/>.</returns>
    private static PluginManifestViolation? ReadDocument(
        string root,
        string path,
        string where,
        IReadOnlyDictionary<string, string>? english,
        long maxViewBytes,
        out JsonElement document,
        out DocumentScan scan)
    {
        document = default;
        scan = new DocumentScan();

        if (!IsPackageRelative(path))
        {
            return new PluginManifestViolation(PluginManifestRule.ViewMissing, $"{where} is not a package-relative {ViewExtension} path");
        }

        var full = Path.GetFullPath(Path.Combine(root, path));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return new PluginManifestViolation(PluginManifestRule.ViewMissing, $"{where} does not exist in the plugin folder");
        }

        var located = CheckLocation(root, path, full, where);
        if (located is not null)
        {
            return located;
        }

        try
        {
            // One handle for the size and the read, so the length checked is the length of the file that is read.
            using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);

            // A regular file is seekable; a FIFO, socket or character device is not, and its length means nothing.
            if (!stream.CanSeek)
            {
                return new PluginManifestViolation(PluginManifestRule.ViewInvalid, $"{where} is not a regular file");
            }

            // The length is checked before anything is read, so an oversized document costs no allocation; the read
            // itself is bounded again because the file may grow between the check and the read.
            if (stream.Length > maxViewBytes)
            {
                return OverSizeLimit(where, maxViewBytes);
            }

            var bytes = ReadBounded(stream, maxViewBytes);
            if (bytes is null)
            {
                return OverSizeLimit(where, maxViewBytes);
            }

            using var parsed = JsonDocument.Parse(bytes, ParseOptions);
            var element = parsed.RootElement;
            var malformed = CheckShape(element) ?? CheckBlocks(element.GetProperty(BlocksMember));
            if (malformed is null)
            {
                Walk(element, scan);
                malformed = scan.Malformed;
            }

            if (malformed is not null)
            {
                return new PluginManifestViolation(PluginManifestRule.ViewInvalid, $"{where} is not a valid view document: {malformed}");
            }

            if (element.GetProperty(SchemaMember).GetInt32() != PluginViews.SchemaVersion)
            {
                return new PluginManifestViolation(
                    PluginManifestRule.ViewSchemaUnsupported, $"{where} has schema {element.GetProperty(SchemaMember)}, this host reads {PluginViews.SchemaVersion}");
            }

            var kind = scan.Kinds.FirstOrDefault(k => !PluginViews.BlockKinds.Contains(k, StringComparer.Ordinal));
            if (kind is not null)
            {
                return new PluginManifestViolation(PluginManifestRule.ViewBlockKindUnknown, $"{where} has a block of kind '{kind}'");
            }

            var state = CheckState(scan);
            if (state is not null)
            {
                return new PluginManifestViolation(state.Rule, $"{where} {state.Detail}");
            }

            foreach (var label in scan.Labels.Where(PluginManifestValidator.IsTextKey))
            {
                var missing = PluginManifestValidator.CheckKeyDefined(where, label, english);
                if (missing is not null)
                {
                    return missing;
                }
            }

            document = element.Clone();
            return null;
        }
        catch (JsonException ex)
        {
            // The type only: a parser message quotes the document, and a detail is shown to the installing admin.
            return new PluginManifestViolation(PluginManifestRule.ViewInvalid, $"{where} is not valid JSON ({ex.GetType().Name})");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The type only: an I/O message carries the absolute host path of the file.
            return new PluginManifestViolation(PluginManifestRule.ViewMissing, $"{where} cannot be read ({ex.GetType().Name})");
        }
    }

    /// <summary>
    /// Checks that the document is reached without a link: no folder between the plugin folder and the file is a
    /// symbolic link or junction, and the file itself is not one. A link would let a view read a file outside the
    /// package. The check is on the path, not the opened handle; a path swapped for a link between the two would be
    /// followed, which is accepted for the reason <see cref="PluginSignaturePayload"/> gives: the plugin folder is
    /// created by the host from archive entries, which are never written as links, and only the host writes there.
    /// </summary>
    /// <param name="root">The full path of the plugin folder.</param>
    /// <param name="path">The package-relative path, already checked by <see cref="IsPackageRelative"/>.</param>
    /// <param name="full">The full path of the file.</param>
    /// <param name="where">The entry naming the document, for the log.</param>
    /// <returns>The violation, or <see langword="null"/> when the file exists and nothing on the way is a link.</returns>
    private static PluginManifestViolation? CheckLocation(string root, string path, string full, string where)
    {
        var segments = path.Split(PackagePathSeparator);
        var current = root;
        foreach (var segment in segments.Take(segments.Length - 1))
        {
            current = Path.Combine(current, segment);
            var folder = new DirectoryInfo(current);
            if (folder.LinkTarget is not null)
            {
                return new PluginManifestViolation(PluginManifestRule.ViewInvalid, $"{where} is reached through a link");
            }

            if (!folder.Exists)
            {
                return new PluginManifestViolation(PluginManifestRule.ViewMissing, $"{where} does not exist in the plugin folder");
            }
        }

        var file = new FileInfo(full);
        if (file.LinkTarget is not null)
        {
            return new PluginManifestViolation(PluginManifestRule.ViewInvalid, $"{where} is a link");
        }

        return file.Exists
            ? null
            : new PluginManifestViolation(PluginManifestRule.ViewMissing, $"{where} does not exist in the plugin folder");
    }

    /// <summary>The violation for a document over the size limit.</summary>
    /// <param name="where">The entry naming the document, for the log.</param>
    /// <param name="maxViewBytes">The limit, in bytes.</param>
    /// <returns>A <see cref="PluginManifestRule.ViewInvalid"/> violation.</returns>
    private static PluginManifestViolation OverSizeLimit(string where, long maxViewBytes)
    {
        return new PluginManifestViolation(
            PluginManifestRule.ViewInvalid, $"{where} is over the size limit of {maxViewBytes} bytes for a view document");
    }

    /// <summary>Reads an open file, never more than <paramref name="maxViewBytes"/> plus one byte of it.</summary>
    /// <param name="stream">The opened file.</param>
    /// <param name="maxViewBytes">The largest content accepted, in bytes.</param>
    /// <returns>The content, or <see langword="null"/> when the file holds more than <paramref name="maxViewBytes"/> bytes.</returns>
    /// <exception cref="IOException">The file cannot be read.</exception>
    private static byte[]? ReadBounded(Stream stream, long maxViewBytes)
    {
        using var content = new MemoryStream();
        var chunk = new byte[ReadChunkBytes];
        while (true)
        {
            // Never ask for more than one byte past the cap, so the read stops there whatever the file holds.
            var wanted = (int)Math.Min(chunk.Length, maxViewBytes + 1 - content.Length);
            var read = stream.Read(chunk, 0, wanted);
            if (read == 0)
            {
                return content.ToArray();
            }

            content.Write(chunk, 0, read);
            if (content.Length > maxViewBytes)
            {
                return null;
            }
        }
    }

    /// <summary>Tells whether a path is forward-slash segments of safe characters ending in <c>.json</c>, so it cannot leave the plugin folder.</summary>
    /// <param name="path">The path a page or slot names.</param>
    /// <returns><see langword="true"/> when the path is package-relative.</returns>
    private static bool IsPackageRelative(string path)
    {
        return path.EndsWith(ViewExtension, StringComparison.Ordinal)
            && path.Split(PackagePathSeparator).All(segment => PathSegmentPattern().IsMatch(segment));
    }

    /// <summary>Checks the root: an object with an integer <c>schema</c> and a <c>blocks</c> array.</summary>
    /// <param name="root">The parsed root.</param>
    /// <returns>What is wrong, or <see langword="null"/>.</returns>
    private static string? CheckShape(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return "the root is not an object";
        }

        if (!root.TryGetProperty(SchemaMember, out var schema) || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out _))
        {
            return $"'{SchemaMember}' is missing or not an integer";
        }

        return root.TryGetProperty(BlocksMember, out var blocks) && blocks.ValueKind == JsonValueKind.Array
            ? null
            : $"'{BlocksMember}' is missing or not an array";
    }

    /// <summary>
    /// Checks the block structure the panel renders: every entry of a <c>blocks</c> array (the root's, a tab's) and of a
    /// block's <c>actions</c> array is an object with a string <c>kind</c>, and a block's <c>tabs</c> is an array of
    /// objects whose <c>blocks</c>, when present, are checked the same way. A block the panel would skip or crash on is
    /// refused at install rather than rendered blank.
    /// </summary>
    /// <param name="blocks">An array of blocks.</param>
    /// <returns>What is wrong, or <see langword="null"/>.</returns>
    private static string? CheckBlocks(JsonElement blocks)
    {
        if (blocks.ValueKind != JsonValueKind.Array)
        {
            return $"'{BlocksMember}' is not an array";
        }

        foreach (var block in blocks.EnumerateArray())
        {
            var problem = CheckBlock(block);
            if (problem is not null)
            {
                return problem;
            }
        }

        return null;
    }

    /// <summary>Checks one block and the blocks nested in its <c>tabs</c> and <c>actions</c>.</summary>
    /// <param name="block">The block.</param>
    /// <returns>What is wrong, or <see langword="null"/>.</returns>
    private static string? CheckBlock(JsonElement block)
    {
        if (block.ValueKind != JsonValueKind.Object)
        {
            return "a block is not an object";
        }

        if (!block.TryGetProperty(KindMember, out var kind) || kind.ValueKind != JsonValueKind.String)
        {
            return "a block has no string 'kind'";
        }

        if (block.TryGetProperty(ActionsMember, out var actions))
        {
            if (actions.ValueKind != JsonValueKind.Array)
            {
                return $"'{ActionsMember}' is not an array";
            }

            var problem = CheckBlocks(actions);
            if (problem is not null)
            {
                return problem;
            }
        }

        if (!block.TryGetProperty(TabsMember, out var tabs))
        {
            return null;
        }

        if (tabs.ValueKind != JsonValueKind.Array)
        {
            return $"'{TabsMember}' is not an array";
        }

        foreach (var tab in tabs.EnumerateArray())
        {
            if (tab.ValueKind != JsonValueKind.Object)
            {
                return "a tab is not an object";
            }

            if (tab.TryGetProperty(BlocksMember, out var tabBlocks))
            {
                var problem = CheckBlocks(tabBlocks);
                if (problem is not null)
                {
                    return problem;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Tells whether a link value is safe for the panel to navigate to or load: a host-relative path (starts with one
    /// <c>/</c>, not <c>//</c>) that does not reach the host API, or an absolute <c>https</c> URL without user info.
    /// Neither may hold a backslash or a control character, which browsers strip or reinterpret before resolving the URL.
    /// </summary>
    /// <param name="link">The link text.</param>
    /// <returns><see langword="true"/> when the link is safe.</returns>
    private static bool IsSafeLink(string link)
    {
        if (link.Length == 0 || link.Contains('\\') || link.Any(char.IsControl))
        {
            return false;
        }

        if (link.StartsWith(PackagePathSeparator))
        {
            return !link.StartsWith(ProtocolRelativePrefix, StringComparison.Ordinal) && !ReachesApi(link);
        }

        return Uri.TryCreate(link, UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, SafeAbsoluteScheme, StringComparison.Ordinal)
            && link.StartsWith(SafeAbsoluteScheme + Uri.SchemeDelimiter, StringComparison.OrdinalIgnoreCase)
            && uri.UserInfo.Length == 0
            && uri.Host.Length > 0;
    }

    /// <summary>
    /// Tells whether a host-relative link would make the browser request the host API (<c>/api</c> or anything under
    /// <c>/api/</c>, any case): a view link is navigation, and pointing it at the API would turn a click into a
    /// request carrying the viewer's session. The path is read as the browser resolves it (dot segments removed),
    /// both as written and with its percent escapes decoded once, because the server decodes the path before it routes.
    /// A link that cannot be resolved counts as reaching the API, so the check fails closed.
    /// </summary>
    /// <param name="link">A host-relative link (starting with one <c>/</c>).</param>
    /// <returns><see langword="true"/> when the link reaches the API or cannot be resolved.</returns>
    private static bool ReachesApi(string link)
    {
        return ResolvesToApi(link) || ResolvesToApi(Uri.UnescapeDataString(link));
    }

    /// <summary>Resolves one spelling of a host-relative link and tells whether its decoded path is the API's.</summary>
    /// <param name="link">The link spelling.</param>
    /// <returns><see langword="true"/> when the path is under the API, or the link cannot be resolved.</returns>
    private static bool ResolvesToApi(string link)
    {
        if (!Uri.TryCreate(new Uri(LinkResolutionBase), link, out var resolved))
        {
            return true;
        }

        var path = Uri.UnescapeDataString(resolved.AbsolutePath);
        return string.Equals(path, ApiPath, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(ApiPathPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Walks a document at any depth, collecting block kinds, ids, labels and routes, checking the view state members,
    /// and noting the first malformed member. A member name used twice in one object is malformed: the parser keeps
    /// both, but the node model the page is served from throws on a duplicate, which would turn a served page into a
    /// server error.
    /// </summary>
    /// <param name="element">The element to walk.</param>
    /// <param name="scan">Receives what is found.</param>
    /// <param name="via">The name of the member holding <paramref name="element"/> (through any arrays), or <see langword="null"/> at the root.</param>
    private static void Walk(JsonElement element, DocumentScan scan, string? via = null)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item, scan, via);
                }

                break;
            case JsonValueKind.Object:
                var kind = element.TryGetProperty(KindMember, out var kindValue) && kindValue.ValueKind == JsonValueKind.String
                    ? kindValue.GetString()
                    : null;
                if (kind is not null
                    && element.TryGetProperty(IdMember, out var id)
                    && id.ValueKind == JsonValueKind.String
                    && id.GetString() is { } blockId)
                {
                    // Containers are walked like the root, so a repeat is found at any depth: in a tab, a pane or a
                    // header. The id is named: it is the plugin author's own text from a file they ship, and the
                    // detail goes to the installing admin or the log.
                    if (!scan.BlockIds.Add(blockId))
                    {
                        scan.Advisories.Add(new PluginManifestViolation(
                            PluginManifestRule.ViewBlockIdRepeated,
                            $"gives the id '{blockId}' to more than one block"));
                    }
                }

                CheckArrangement(element, kind, scan);

                // A split counts itself while its own members are walked, so a split in one of its panes, however deep,
                // is seen by the check above.
                var isSplit = string.Equals(kind, PluginViews.SplitKind, StringComparison.Ordinal);
                scan.SplitDepth += isSplit ? 1 : 0;

                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var member in element.EnumerateObject())
                {
                    if (!names.Add(member.Name))
                    {
                        scan.Malformed ??= $"the member '{member.Name}' appears twice in one object";
                    }

                    if (!WalkStateMember(member, kind, string.Equals(via, SubmitMember, StringComparison.Ordinal), scan))
                    {
                        WalkMember(member, scan);
                    }
                }

                scan.SplitDepth -= isSplit ? 1 : 0;
                break;
        }
    }

    /// <summary>Checks a block's shape when it is a <c>split</c> or a <c>preview</c>.</summary>
    /// <param name="block">The object being walked.</param>
    /// <param name="kind">Its block kind, or <see langword="null"/> when it is no block.</param>
    /// <param name="scan">Receives the first broken rule.</param>
    private static void CheckArrangement(JsonElement block, string? kind, DocumentScan scan)
    {
        CheckDisplayMembers(block, kind, scan);

        if (string.Equals(kind, PluginViews.SplitKind, StringComparison.Ordinal))
        {
            CheckSplit(block, scan);
        }
        else if (string.Equals(kind, PluginViews.PreviewKind, StringComparison.Ordinal))
        {
            scan.HasPreview = true;
            CheckPreview(block, scan);
            CheckPreviewSelection(block, scan);
        }
        else if (string.Equals(kind, ChatKind, StringComparison.Ordinal) && block.TryGetProperty(SelectionMember, out var chatSelection))
        {
            scan.HasChatSelection = true;
            if (chatSelection.ValueKind != JsonValueKind.True)
            {
                scan.StateViolation ??= new PluginManifestViolation(
                    PluginManifestRule.ViewSelectionInvalid, $"has a '{ChatKind}' whose '{SelectionMember}' is anything but true");
            }
        }
        else if (string.Equals(kind, ActionKind, StringComparison.Ordinal) && block.TryGetProperty(BodySourceMember, out var bodySource))
        {
            scan.HasActionBody = true;
            CheckActionBody(block, bodySource, scan);
        }
    }

    /// <summary>
    /// Records a fault of a rule that was added after plugins were installed. Such a fault refuses the plugin at
    /// install (<see cref="PluginViewRead.RefusedAtInstall"/>) and is only warned about at load
    /// (<see cref="PluginViewRead.Advisories"/>): a plugin the operator already runs, and that may take payments,
    /// must not vanish from production on a host upgrade because the host learned to check one more thing.
    /// </summary>
    /// <param name="scan">Receives the advisory.</param>
    /// <param name="text">What is malformed, without the document's place.</param>
    private static void AddAdvisory(DocumentScan scan, string text)
    {
        scan.Advisories.Add(
            new PluginManifestViolation(PluginManifestRule.ViewInvalid, $"is not a valid view document: {text}"));
    }

    /// <summary>
    /// Checks the members that shape how a block reads and draws itself, which the panel would otherwise ignore when
    /// they are of the wrong type: a header's <c>compact</c> (a boolean), <c>titleSource</c> and <c>subtitleSource</c>,
    /// an action's <c>labelSource</c>, a preview's <c>note</c> (each a GET route and, when given, a dotted
    /// <c>field</c>) and a table's <c>display</c> (<c>table</c> or <c>cards</c>). Each member is checked on the one
    /// kind that reads it, so a view that carries it elsewhere is left as it was. A route is declared in the area by
    /// the ordinary walk. A fault here is an advisory (see <see cref="AddAdvisory"/>), not a refusal of the view at
    /// load.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <param name="kind">The block's kind, or <see langword="null"/>.</param>
    /// <param name="scan">Receives each malformed member as an advisory.</param>
    private static void CheckDisplayMembers(JsonElement block, string? kind, DocumentScan scan)
    {
        if (string.Equals(kind, HeaderKind, StringComparison.Ordinal))
        {
            if (block.TryGetProperty(CompactMember, out var compact)
                && compact.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                AddAdvisory(scan, $"'{CompactMember}' is not a boolean");
            }

            CheckSourceMember(block, TitleSourceMember, scan);
            CheckSourceMember(block, SubtitleSourceMember, scan);
        }
        else if (string.Equals(kind, ActionKind, StringComparison.Ordinal))
        {
            CheckSourceMember(block, LabelSourceMember, scan);
        }
        else if (string.Equals(kind, PluginViews.PreviewKind, StringComparison.Ordinal))
        {
            CheckSourceMember(block, NoteMember, scan);
        }
        else if (string.Equals(kind, TableKind, StringComparison.Ordinal)
            && block.TryGetProperty(DisplayMember, out var display)
            && !(display.ValueKind == JsonValueKind.String
                && TableDisplays.Contains(display.GetString(), StringComparer.Ordinal)))
        {
            AddAdvisory(scan, $"'{DisplayMember}' is not one of {string.Join(", ", TableDisplays)}");
        }
    }

    /// <summary>
    /// Checks one optional member that is a source: an object with a GET <c>route</c> and, when it has one, a dotted
    /// <c>field</c> that is a string of the shape a condition's field has.
    /// </summary>
    /// <param name="block">The block holding the member.</param>
    /// <param name="member">The member's name.</param>
    /// <param name="scan">Receives each malformed member as an advisory.</param>
    private static void CheckSourceMember(JsonElement block, string member, DocumentScan scan)
    {
        if (!block.TryGetProperty(member, out var source))
        {
            return;
        }

        if (source.ValueKind != JsonValueKind.Object)
        {
            AddAdvisory(scan, $"'{member}' is not an object");
            return;
        }

        var route = source.TryGetProperty(ConditionRouteMember, out var routeValue) ? ReadRoute(routeValue) : null;
        var fieldIsValid = !source.TryGetProperty(ConditionFieldMember, out var field)
            || (field.ValueKind == JsonValueKind.String
                && field.GetString() is { Length: <= MaxConditionFieldLength } path
                && ConditionFieldPattern().IsMatch(path));
        if (route is null || route.Method != PluginRouteMethod.Get || !fieldIsValid)
        {
            AddAdvisory(
                scan,
                $"'{member}' is not {{ {ConditionRouteMember}: a GET route, {ConditionFieldMember}: a dotted path }}");
        }
    }

    /// <summary>
    /// Checks an action's body source: an object holding exactly a GET <c>route</c> and 1 to
    /// <see cref="MaxBodySourceFields"/> <c>fields</c>, each a body member and the dotted path it is read from. The action
    /// has an <c>id</c>, so a refresh can read it again, and calls a method that carries a body. A
    /// <c>labelSource</c> names the same route, because the panel then takes the label and the body from one reply: two
    /// reads could answer differently, and the button would send a value other than the one on its label.
    /// </summary>
    /// <param name="action">The action block.</param>
    /// <param name="value">The body source.</param>
    /// <param name="scan">Receives the route and the first broken rule.</param>
    private static void CheckActionBody(JsonElement action, JsonElement value, DocumentScan scan)
    {
        var route = value.ValueKind == JsonValueKind.Object && value.TryGetProperty(ConditionRouteMember, out var routeValue)
            ? ReadRoute(routeValue)
            : null;
        var called = action.TryGetProperty(ActionRouteMember, out var calledValue) ? ReadRoute(calledValue) : null;
        var valid = route is not null
            && route.Method == PluginRouteMethod.Get
            && value.EnumerateObject().Count() == BodySourceMemberCount
            && value.TryGetProperty(BodySourceFieldsMember, out var fields)
            && AreBodyFields(fields)
            && action.TryGetProperty(IdMember, out var id)
            && id.ValueKind == JsonValueKind.String
            && !string.IsNullOrEmpty(id.GetString())
            && called?.Method is PluginRouteMethod.Post or PluginRouteMethod.Put or PluginRouteMethod.Patch
            && LabelReadsTheSameRoute(action, route);
        if (route is null || !valid)
        {
            scan.StateViolation ??= new PluginManifestViolation(
                PluginManifestRule.ViewActionBodyInvalid,
                $"has a '{BodySourceMember}' that is not {{ {ConditionRouteMember}: a GET route, {BodySourceFieldsMember}: 1 to {MaxBodySourceFields} members and dotted paths }} on an action with an id that calls POST, PUT or PATCH and reads its label from that route");
            return;
        }

        scan.Routes.Add(route);
    }

    /// <summary>
    /// Tells whether a body source's <c>fields</c> are 1 to <see cref="MaxBodySourceFields"/> members, each named once and
    /// not like a label member, which the localiser would rewrite when the page is served.
    /// </summary>
    /// <param name="fields">The value.</param>
    /// <returns><see langword="true"/> when they are.</returns>
    private static bool AreBodyFields(JsonElement fields)
    {
        if (fields.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var members = fields.EnumerateObject().ToList();
        return members.Count is >= 1 and <= MaxBodySourceFields
            && members.Select(member => member.Name).Distinct(StringComparer.Ordinal).Count() == members.Count
            && members.TrueForAll(member =>
                BodyMemberPattern().IsMatch(member.Name)
                && !PluginViews.LabelProperties.Contains(member.Name, StringComparer.Ordinal)
                && member.Value.ValueKind == JsonValueKind.String
                && member.Value.GetString() is { Length: <= MaxConditionFieldLength } path
                && ConditionFieldPattern().IsMatch(path));
    }

    /// <summary>Tells whether an action with a body source has no label source, or one reading the same route.</summary>
    /// <param name="action">The action block.</param>
    /// <param name="route">The body source's route.</param>
    /// <returns><see langword="true"/> when the label and the body come from one read.</returns>
    private static bool LabelReadsTheSameRoute(JsonElement action, PluginViewRoute route)
    {
        if (!action.TryGetProperty(LabelSourceMember, out var label))
        {
            return true;
        }

        var labelRoute = label.ValueKind == JsonValueKind.Object && label.TryGetProperty(ConditionRouteMember, out var labelValue)
            ? ReadRoute(labelValue)
            : null;
        return labelRoute is not null
            && labelRoute.Method == route.Method
            && string.Equals(labelRoute.Path, route.Path, StringComparison.Ordinal);
    }

    /// <summary>
    /// Checks a split: not inside another split, with exactly <see cref="PluginViews.SplitPaneCount"/> panes, each an object
    /// holding only a valid <c>blocks</c> array. A pane the panel could not render would leave half a screen blank, so it
    /// is refused at install; what the panes hold is walked like any other content.
    /// </summary>
    /// <param name="split">The split block.</param>
    /// <param name="scan">Receives the first broken rule.</param>
    private static void CheckSplit(JsonElement split, DocumentScan scan)
    {
        var valid = scan.SplitDepth == 0
            && split.TryGetProperty(PluginViews.PanesProperty, out var panes)
            && panes.ValueKind == JsonValueKind.Array
            && panes.GetArrayLength() == PluginViews.SplitPaneCount
            && panes.EnumerateArray().All(IsPane);
        if (!valid)
        {
            scan.StateViolation ??= new PluginManifestViolation(
                PluginManifestRule.ViewSplitInvalid,
                $"has a '{PluginViews.SplitKind}' that is nested or does not have {PluginViews.SplitPaneCount} panes, each holding only '{BlocksMember}'");
        }
    }

    /// <summary>Tells whether a pane is an object holding only a valid <c>blocks</c> array.</summary>
    /// <param name="pane">The pane.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsPane(JsonElement pane)
    {
        return pane.ValueKind == JsonValueKind.Object
            && pane.EnumerateObject().Count() == PaneMemberCount
            && pane.TryGetProperty(BlocksMember, out var blocks)
            && CheckBlocks(blocks) is null;
    }

    /// <summary>
    /// Checks a preview's <c>sources</c> and <c>size</c>. The routes and labels of the sources are collected by the ordinary
    /// walk, so a route must also be declared in the area and a label key must exist. The address itself is only known
    /// when the panel reads it, so its origin is checked there, against what the host allows for that reader.
    /// </summary>
    /// <param name="preview">The preview block.</param>
    /// <param name="scan">Receives the first broken rule.</param>
    private static void CheckPreview(JsonElement preview, DocumentScan scan)
    {
        var sizeValid = !preview.TryGetProperty(PluginViews.PreviewSizeProperty, out var size)
            || (size.ValueKind == JsonValueKind.String
                && PluginViews.PreviewSizes.Contains(size.GetString() ?? string.Empty, StringComparer.Ordinal));
        var sourcesValid = preview.TryGetProperty(PluginViews.PreviewSourcesProperty, out var sources)
            && sources.ValueKind == JsonValueKind.Array
            && sources.GetArrayLength() is >= 1 and <= PluginViews.MaxPreviewSources
            && sources.EnumerateArray().All(IsPreviewSource)
            && sources.EnumerateArray()
                .Select(source => source.GetProperty(PreviewSourceIdMember).GetString())
                .Distinct(StringComparer.Ordinal)
                .Count() == sources.GetArrayLength();
        if (!sizeValid || !sourcesValid)
        {
            scan.StateViolation ??= new PluginManifestViolation(
                PluginManifestRule.ViewPreviewInvalid,
                $"has a '{PluginViews.PreviewKind}' whose '{PluginViews.PreviewSourcesProperty}' or '{PluginViews.PreviewSizeProperty}' is not valid");
        }
    }

    /// <summary>
    /// Checks a preview's <c>selection</c>, off when absent: exactly the sources that may select (declared by the block, so a
    /// plugin must name them and a live source can stay out), a POST route that looks a claimed selection up, and a GET route
    /// and field reading the release on the preview. A message from a frame is only ever measured against these. One preview
    /// per document may offer it, because a chat's claim names a release and ids and not a preview, so the plugin could not
    /// tell two apart when their releases are equal.
    /// </summary>
    /// <param name="preview">The preview block.</param>
    /// <param name="scan">Receives the first broken rule.</param>
    private static void CheckPreviewSelection(JsonElement preview, DocumentScan scan)
    {
        if (!preview.TryGetProperty(SelectionMember, out var selection))
        {
            return;
        }

        var second = scan.HasPreviewSelection;
        scan.HasPreviewSelection = true;
        if (second || !IsPreviewSelection(preview, selection))
        {
            scan.StateViolation ??= new PluginManifestViolation(
                PluginManifestRule.ViewSelectionInvalid, $"has a '{PluginViews.PreviewKind}' whose '{SelectionMember}' is not valid or is the document's second");
        }
    }

    /// <summary>Tells whether a preview's selection holds exactly its three members, each in its shape.</summary>
    /// <param name="preview">The preview block.</param>
    /// <param name="selection">Its selection.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsPreviewSelection(JsonElement preview, JsonElement selection)
    {
        if (selection.ValueKind != JsonValueKind.Object
            || selection.EnumerateObject().Count() != SelectionMemberCount
            || !selection.TryGetProperty(ConditionRouteMember, out var route)
            || ReadRoute(route) is not { Method: PluginRouteMethod.Post }
            || !selection.TryGetProperty(SelectionReleaseMember, out var release)
            || release.ValueKind != JsonValueKind.Object
            || release.EnumerateObject().Count() != SelectionReleaseMemberCount
            || !release.TryGetProperty(ConditionRouteMember, out var releaseRoute)
            || ReadRoute(releaseRoute) is not { Method: PluginRouteMethod.Get }
            || !release.TryGetProperty(ConditionFieldMember, out var field)
            || field.ValueKind != JsonValueKind.String
            || field.GetString() is not { Length: <= MaxConditionFieldLength } path
            || !ConditionFieldPattern().IsMatch(path)
            || !selection.TryGetProperty(SelectionSourcesMember, out var sources)
            || sources.ValueKind != JsonValueKind.Array
            || sources.GetArrayLength() == 0
            || !preview.TryGetProperty(PluginViews.PreviewSourcesProperty, out var declared)
            || declared.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        // The sources have not been validated yet when this runs, and GetString throws on anything but a string.
        var known = declared.EnumerateArray()
            .Where(source => source.ValueKind == JsonValueKind.Object
                && source.TryGetProperty(PreviewSourceIdMember, out var id)
                && id.ValueKind == JsonValueKind.String)
            .Select(source => source.GetProperty(PreviewSourceIdMember).GetString())
            .ToHashSet(StringComparer.Ordinal);
        var named = sources.EnumerateArray().Select(source => source.ValueKind == JsonValueKind.String ? source.GetString() : null).ToList();
        return named.All(id => id is not null && known.Contains(id)) && named.Distinct(StringComparer.Ordinal).Count() == named.Count;
    }

    /// <summary>Refuses a chat that takes a selection when no preview of its document offers one.</summary>
    /// <param name="use">The page or slot showing the document.</param>
    /// <param name="scan">What the walk found.</param>
    /// <returns>The violation, or <see langword="null"/>.</returns>
    private static PluginManifestViolation? CheckSelectionPlace(ViewUse use, DocumentScan scan)
    {
        return scan.HasChatSelection && !scan.HasPreviewSelection
            ? new PluginManifestViolation(
                PluginManifestRule.ViewSelectionInvalid, $"{use.Where} has a '{ChatKind}' taking a selection no preview offers")
            : null;
    }

    /// <summary>Tells whether a preview source holds exactly an id, a label, a GET route and a dotted field.</summary>
    /// <param name="source">The source.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsPreviewSource(JsonElement source)
    {
        return source.ValueKind == JsonValueKind.Object
            && source.EnumerateObject().Count() == PreviewSourceMemberCount
            && source.TryGetProperty(PreviewSourceIdMember, out var id)
            && id.ValueKind == JsonValueKind.String
            && id.GetString() is { Length: <= PluginViews.MaxPreviewSourceIdLength } name
            && PreviewSourceIdPattern().IsMatch(name)
            && source.TryGetProperty(PreviewSourceLabelMember, out var label)
            && label.ValueKind == JsonValueKind.String
            && !string.IsNullOrEmpty(label.GetString())
            && source.TryGetProperty(ConditionRouteMember, out var route)
            && ReadRoute(route) is { Method: PluginRouteMethod.Get }
            && source.TryGetProperty(ConditionFieldMember, out var field)
            && field.ValueKind == JsonValueKind.String
            && field.GetString() is { Length: <= MaxConditionFieldLength } path
            && ConditionFieldPattern().IsMatch(path);
    }

    /// <summary>
    /// Refuses a preview anywhere but on a client page. Only the document of a client plugin page gets the host's narrowed
    /// <c>frame-src</c>, and only there is the reader's own account known to the origins; elsewhere the block could not be
    /// held to them by the browser.
    /// </summary>
    /// <param name="use">The page or slot showing the document.</param>
    /// <param name="scan">What the walk found.</param>
    /// <returns>The violation, or <see langword="null"/>.</returns>
    private static PluginManifestViolation? CheckPreviewPlace(ViewUse use, DocumentScan scan)
    {
        return scan.HasPreview && (!use.IsPage || use.Area != PluginArea.Client)
            ? new PluginManifestViolation(
                PluginManifestRule.ViewPreviewInvalid, $"{use.Where} shows a '{PluginViews.PreviewKind}', which only a client page may")
            : null;
    }

    /// <summary>
    /// Handles a view state member (<see cref="PluginViews.VisibleWhenProperty"/>, <see cref="PluginViews.ResetOnSuccessProperty"/>,
    /// <see cref="PluginViews.FollowUpProperty"/>) where it is allowed, and records a broken rule where it is not. A
    /// state member is never walked as ordinary content, so a value in a condition's <c>equals</c> is never read as a label.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <param name="kind">The block kind of the object holding it, or <see langword="null"/> when that object is no block.</param>
    /// <param name="inSubmit">Whether the object holding it is a form's <c>submit</c>.</param>
    /// <param name="scan">Receives what is found.</param>
    /// <returns><see langword="true"/> when the member is a state member and was handled here.</returns>
    private static bool WalkStateMember(JsonProperty member, string? kind, bool inSubmit, DocumentScan scan)
    {
        if (member.NameEquals(PluginViews.VisibleWhenProperty))
        {
            if (kind is null)
            {
                scan.StateViolation ??= new PluginManifestViolation(
                    PluginManifestRule.ViewConditionInvalid, $"has a '{PluginViews.VisibleWhenProperty}' on something that is not a block");
            }
            else
            {
                CheckCondition(member.Value, PluginViews.VisibleWhenProperty, scan);
            }

            return true;
        }

        if (member.NameEquals(BodySourceMember))
        {
            // On an action it was checked with its block; its field names are never content, so it is not walked.
            if (!string.Equals(kind, ActionKind, StringComparison.Ordinal))
            {
                scan.StateViolation ??= new PluginManifestViolation(
                    PluginManifestRule.ViewActionBodyInvalid, $"has a '{BodySourceMember}' outside an action block");
            }

            return true;
        }

        if (member.NameEquals(PluginViews.ResetOnSuccessProperty))
        {
            if (!inSubmit || (member.Value.ValueKind != JsonValueKind.True && member.Value.ValueKind != JsonValueKind.False))
            {
                scan.StateViolation ??= new PluginManifestViolation(
                    PluginManifestRule.ViewFollowUpInvalid, $"has a '{PluginViews.ResetOnSuccessProperty}' that is not a boolean in a form's '{SubmitMember}'");
            }

            return true;
        }

        if (member.NameEquals(PluginViews.FollowUpProperty))
        {
            if (!inSubmit && !string.Equals(kind, ActionKind, StringComparison.Ordinal))
            {
                scan.StateViolation ??= new PluginManifestViolation(
                    PluginManifestRule.ViewFollowUpInvalid, $"has a '{PluginViews.FollowUpProperty}' outside an action block and a form's '{SubmitMember}'");
            }
            else
            {
                CheckFollowUp(member.Value, scan);
            }

            return true;
        }

        return false;
    }

    /// <summary>Refuses a body source anywhere but in the client area, the one panel whose action block sends it.</summary>
    /// <param name="use">Where the document is shown.</param>
    /// <param name="scan">What the document holds.</param>
    /// <returns>The violation, or <see langword="null"/>.</returns>
    private static PluginManifestViolation? CheckActionBodyPlace(ViewUse use, DocumentScan scan)
    {
        return scan.HasActionBody && use.Area != PluginArea.Client
            ? new PluginManifestViolation(
                PluginManifestRule.ViewActionBodyInvalid, $"{use.Where} has an action with a '{BodySourceMember}', which only the client area sends")
            : null;
    }

    /// <summary>
    /// Checks a follow-up: an object holding exactly <c>refresh</c>, a list of 1 to
    /// <see cref="PluginViews.MaxFollowUpRefresh"/> non-empty block ids, and <c>until</c>, a condition. Whether each id
    /// names a block of the document is checked once the whole document is walked (<see cref="CheckState"/>).
    /// </summary>
    /// <param name="value">The follow-up value.</param>
    /// <param name="scan">Receives the ids and the first broken rule.</param>
    private static void CheckFollowUp(JsonElement value, DocumentScan scan)
    {
        if (value.ValueKind != JsonValueKind.Object
            || value.EnumerateObject().Count() != FollowUpMemberCount
            || !value.TryGetProperty(FollowUpRefreshMember, out var refresh)
            || !value.TryGetProperty(FollowUpUntilMember, out var until)
            || refresh.ValueKind != JsonValueKind.Array
            || refresh.GetArrayLength() is < 1 or > PluginViews.MaxFollowUpRefresh
            || refresh.EnumerateArray().Any(id => id.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(id.GetString())))
        {
            scan.StateViolation ??= new PluginManifestViolation(
                PluginManifestRule.ViewFollowUpInvalid,
                $"has a '{PluginViews.FollowUpProperty}' that is not {{ {FollowUpRefreshMember}: 1 to {PluginViews.MaxFollowUpRefresh} block ids, {FollowUpUntilMember}: a condition }}");
            return;
        }

        scan.FollowUpRefresh.AddRange(refresh.EnumerateArray().Select(id => id.GetString() ?? string.Empty));
        CheckCondition(until, $"{PluginViews.FollowUpProperty}.{FollowUpUntilMember}", scan);
    }

    /// <summary>
    /// Checks a condition: an object holding exactly a GET <c>route</c>, a dotted <c>field</c> and an <c>equals</c> that is
    /// one JSON scalar or a list of 1 to <see cref="PluginViews.MaxConditionValues"/> scalars. The route joins the
    /// document's routes, so it must also be declared in the area that shows the view.
    /// </summary>
    /// <param name="value">The condition value.</param>
    /// <param name="member">The member holding it, for the detail.</param>
    /// <param name="scan">Receives the route and the first broken rule.</param>
    private static void CheckCondition(JsonElement value, string member, DocumentScan scan)
    {
        var route = value.ValueKind == JsonValueKind.Object && value.TryGetProperty(ConditionRouteMember, out var routeValue)
            ? ReadRoute(routeValue)
            : null;
        var valid = route is not null
            && route.Method == PluginRouteMethod.Get
            && value.EnumerateObject().Count() == ConditionMemberCount
            && value.TryGetProperty(ConditionFieldMember, out var field)
            && field.ValueKind == JsonValueKind.String
            && field.GetString() is { Length: <= MaxConditionFieldLength } path
            && ConditionFieldPattern().IsMatch(path)
            && value.TryGetProperty(ConditionEqualsMember, out var equals)
            && IsConditionValue(equals);
        if (route is null || !valid)
        {
            scan.StateViolation ??= new PluginManifestViolation(
                PluginManifestRule.ViewConditionInvalid,
                $"has a '{member}' that is not {{ {ConditionRouteMember}: a GET route, {ConditionFieldMember}: a dotted path, {ConditionEqualsMember}: a value or 1 to {PluginViews.MaxConditionValues} values }}");
            return;
        }

        scan.Routes.Add(route);
    }

    /// <summary>Tells whether a condition's <c>equals</c> is one JSON scalar or a list of 1 to <see cref="PluginViews.MaxConditionValues"/> of them.</summary>
    /// <param name="equals">The value.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsConditionValue(JsonElement equals)
    {
        if (equals.ValueKind != JsonValueKind.Array)
        {
            return IsScalar(equals);
        }

        return equals.GetArrayLength() is >= 1 and <= PluginViews.MaxConditionValues && equals.EnumerateArray().All(IsScalar);
    }

    /// <summary>Tells whether a JSON value is a scalar a reply field can be compared with: a string, a number, a boolean or null.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when it is a scalar.</returns>
    private static bool IsScalar(JsonElement value)
    {
        return value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null;
    }

    /// <summary>
    /// Applies the view state rules once the whole document is walked: the first state member broken during the walk,
    /// then a follow-up refresh naming a block id the document does not hold (a refresh of nothing would leave the
    /// reader waiting on a block that never changes). A repeated block id is not checked here: it is an advisory.
    /// </summary>
    /// <param name="scan">What the walk found.</param>
    /// <returns>The violation, its detail without the document's place, or <see langword="null"/>.</returns>
    private static PluginManifestViolation? CheckState(DocumentScan scan)
    {
        if (scan.StateViolation is not null)
        {
            return scan.StateViolation;
        }

        var unknown = scan.FollowUpRefresh.FirstOrDefault(id => !scan.BlockIds.Contains(id));
        return unknown is null
            ? null
            : new PluginManifestViolation(
                PluginManifestRule.ViewFollowUpInvalid, $"has a '{PluginViews.FollowUpProperty}' refreshing the block '{unknown}', which the document does not hold");
    }

    /// <summary>
    /// Handles one member of an object: a refused member, an icon, a link, a <c>kind</c>, a route, a label, or a
    /// container to walk into. The refused, icon and link names are compared ignoring case, so a spelling the panel
    /// or the browser might still honour (<c>HREF</c>, <c>formaction</c>) is checked the same way.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <param name="scan">Receives what is found.</param>
    private static void WalkMember(JsonProperty member, DocumentScan scan)
    {
        if (RefusedMembers.Contains(member.Name, StringComparer.OrdinalIgnoreCase))
        {
            scan.Malformed ??= $"'{member.Name}' is not allowed in a view";
            return;
        }

        if (string.Equals(member.Name, IconMember, StringComparison.OrdinalIgnoreCase))
        {
            if (member.Value.ValueKind != JsonValueKind.String
                || member.Value.GetString() is not { } icon
                || !IconNamePattern().IsMatch(icon))
            {
                scan.Malformed ??= $"'{member.Name}' is not a plain icon name";
            }

            return;
        }

        if (LinkMembers.Contains(member.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (member.Value.ValueKind != JsonValueKind.String
                || member.Value.GetString() is not { } link
                || !IsSafeLink(link))
            {
                scan.Malformed ??= $"'{member.Name}' is not a host-relative path outside the API or an https URL";
            }

            return;
        }

        if (member.NameEquals(KindMember))
        {
            if (member.Value.ValueKind == JsonValueKind.String && member.Value.GetString() is { } kind)
            {
                scan.Kinds.Add(kind);
            }
            else
            {
                scan.Malformed ??= "a block 'kind' is not a string";
            }

            return;
        }

        if (PluginViews.RouteProperties.Contains(member.Name, StringComparer.Ordinal))
        {
            var route = ReadRoute(member.Value);
            if (route is null)
            {
                scan.Malformed ??= $"'{member.Name}' is not a route with a method and a path";
            }
            else
            {
                scan.Routes.Add(route);
            }

            return;
        }

        if (PluginViews.LabelProperties.Contains(member.Name, StringComparer.Ordinal)
            && member.Value.ValueKind == JsonValueKind.String
            && member.Value.GetString() is { } label)
        {
            scan.Labels.Add(label);
            return;
        }

        Walk(member.Value, scan, member.Name);
    }

    /// <summary>Reads the value of a route member.</summary>
    /// <param name="value">The member value.</param>
    /// <returns>The route, or <see langword="null"/> when the value is not an object naming a defined method and a path.</returns>
    private static PluginViewRoute? ReadRoute(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        try
        {
            var route = value.Deserialize<PluginViewRoute>(PluginManifestReader.JsonOptions);
            return route is not null && Enum.IsDefined(route.Method) && route.Path is not null ? route : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Checks that every route a document names is declared in the area that shows it.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <param name="use">The page or slot showing the document.</param>
    /// <param name="scan">The routes the document names.</param>
    /// <returns>The violation, or <see langword="null"/>.</returns>
    private static PluginManifestViolation? CheckRoutes(PluginManifest manifest, ViewUse use, DocumentScan scan)
    {
        var declared = use.Area == PluginArea.Client ? manifest.Api.Client : manifest.Api.Admin;
        var route = scan.Routes.FirstOrDefault(named => !declared.Any(named.Names));
        return route is null
            ? null
            : new PluginManifestViolation(
                PluginManifestRule.ViewRouteUndeclared,
                $"{use.Where} names the route {route.Method.ToString().ToUpperInvariant()} {route.Path}, which the {use.Area.ToString().ToLowerInvariant()} api does not declare");
    }
}
