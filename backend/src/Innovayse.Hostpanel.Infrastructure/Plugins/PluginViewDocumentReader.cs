using System.Text.Json;
using System.Text.RegularExpressions;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Hostpanel.Sdk.Packaging.Signing;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Slots;
using Hostpanel.Sdk.Plugins.Ui;

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

    /// <summary>Reads and checks every view document the manifest names, under the plugin folder.</summary>
    /// <param name="pluginDirectory">The plugin folder: the folder holding <c>plugin.json</c>.</param>
    /// <param name="manifest">The manifest, already accepted by <see cref="PluginManifestValidator.Validate"/>.</param>
    /// <param name="maxViewBytes">The largest view document accepted, in bytes (<c>Plugins:MaxViewBytes</c>).</param>
    /// <returns>The documents, or the first view rule the plugin breaks.</returns>
    internal static PluginViewRead Read(string pluginDirectory, PluginManifest manifest, long maxViewBytes)
    {
        var uses = CollectUses(manifest);
        if (uses.Count == 0)
        {
            return new PluginViewRead(PluginViewDocuments.None, null);
        }

        var root = Path.GetFullPath(pluginDirectory);
        var english = PluginManifestValidator.EnglishTexts(manifest);
        var documents = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        foreach (var group in uses.GroupBy(use => use.Path, StringComparer.Ordinal))
        {
            var first = group.First();
            var violation = ReadDocument(root, group.Key, first.Where, english, maxViewBytes, out var document, out var scan);
            if (violation is not null)
            {
                return new PluginViewRead(PluginViewDocuments.None, violation);
            }

            foreach (var use in group)
            {
                violation = CheckRoutes(manifest, use, scan);
                if (violation is not null)
                {
                    return new PluginViewRead(PluginViewDocuments.None, violation);
                }
            }

            documents[group.Key] = document;
        }

        return new PluginViewRead(new PluginViewDocuments(documents), null);
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
                uses.Add(new ViewUse(page.View, area.Area, $"ui.{area.Member}.pages '{page.Id}' view '{page.View}'"));
            }
        }

        foreach (var slot in manifest.Ui.Slots)
        {
            var known = PluginSlots.Find(slot.Slot);
            if (known is not null)
            {
                uses.Add(new ViewUse(slot.View, known.Area, $"ui.slots '{slot.Id}' view '{slot.View}'"));
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
    /// Walks a document at any depth, collecting block kinds, labels and routes, and noting the first malformed member.
    /// A member name used twice in one object is malformed: the parser keeps both, but the node model the page is
    /// served from throws on a duplicate, which would turn a served page into a server error.
    /// </summary>
    /// <param name="element">The element to walk.</param>
    /// <param name="scan">Receives what is found.</param>
    private static void Walk(JsonElement element, DocumentScan scan)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item, scan);
                }

                break;
            case JsonValueKind.Object:
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var member in element.EnumerateObject())
                {
                    if (!names.Add(member.Name))
                    {
                        scan.Malformed ??= $"the member '{member.Name}' appears twice in one object";
                    }

                    WalkMember(member, scan);
                }

                break;
        }
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

        Walk(member.Value, scan);
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
