using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Manifest;
using Innovayse.SDK.Plugins.Slots;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The one place the host checks a plugin manifest and the assemblies beside it against the manifest rules.
/// Loading a plugin at startup (<see cref="PluginManifestReader.TryRead"/>) and installing an upload
/// (<see cref="PluginPackageStore"/>) both ask it, so a rule is written once and holds on both paths.
/// </summary>
internal static partial class PluginManifestValidator
{
    /// <summary>The locale whose texts must define every text key a manifest uses; the other locales may fall back to it.</summary>
    private const string EnglishLocale = "en";

    /// <summary>The manifest versions this host reads: <c>1</c> (fields only) and <c>2</c> (settings, locales, api, ui).</summary>
    private static readonly IReadOnlySet<int> SupportedManifestVersions = new HashSet<int> { 1, 2 };

    /// <summary>
    /// The permission names a client route, page or slot entry may name: every defined <see cref="ClientPermission"/>
    /// except <see cref="ClientPermission.None"/> (a permission that requires nothing is written
    /// <see cref="PluginPermissions.Any"/>), compared exactly.
    /// </summary>
    private static readonly IReadOnlySet<string> ClientPermissionNames = Enum.GetNames<ClientPermission>()
        .Where(name => name != nameof(ClientPermission.None))
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>The name of the SDK assembly a plugin references, read from the assembly the host itself compiled against.</summary>
    private static readonly string SdkAssemblyName = typeof(PluginManifest).Assembly.GetName().Name!;

    /// <summary>The SDK major version of this host: a plugin built against a lower one cannot load.</summary>
    private static readonly int HostSdkMajorVersion = typeof(PluginManifest).Assembly.GetName().Version!.Major;

    /// <summary>
    /// Matches a text key as the SDK documents it for <c>label</c> and <c>title</c> members: dot separated words
    /// such as <c>menu.reports</c>, with no spaces. Anything else is plain text that is shown as written. The end
    /// is <c>\z</c>, not <c>$</c>, because <c>$</c> also matches before a trailing newline.
    /// </summary>
    [GeneratedRegex(@"\A[A-Za-z][A-Za-z0-9_-]*(\.[A-Za-z0-9_-]+)+\z", RegexOptions.CultureInvariant)]
    private static partial Regex TextKeyPattern();

    /// <summary>Matches one literal route segment: lowercase letters, digits, hyphen and underscore, with nothing after it, not even a newline.</summary>
    [GeneratedRegex(@"\A[a-z0-9][a-z0-9_-]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex LiteralSegmentPattern();

    /// <summary>Matches one route parameter segment such as <c>{id}</c>, with nothing after it, not even a newline.</summary>
    [GeneratedRegex(@"\A\{[A-Za-z][A-Za-z0-9]*\}\z", RegexOptions.CultureInvariant)]
    private static partial Regex ParameterSegmentPattern();

    /// <summary>
    /// Checks the manifest alone: its version, permissions, route paths, text keys and slots. A version 1 manifest
    /// declares none of the blocks these rules look at, so it passes by having nothing to break.
    /// </summary>
    /// <param name="manifest">The manifest, already read.</param>
    /// <returns>The first rule the manifest breaks, or <see langword="null"/> when it meets them all.</returns>
    internal static PluginManifestViolation? Validate(PluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (!SupportedManifestVersions.Contains(manifest.ManifestVersion))
        {
            return new PluginManifestViolation(
                PluginManifestRule.ManifestVersionUnknown,
                $"manifestVersion {manifest.ManifestVersion} is not one of {string.Join(", ", SupportedManifestVersions.Order())}");
        }

        return CheckRoutes(PluginArea.Client, manifest.Api.Client)
            ?? CheckRoutes(PluginArea.Admin, manifest.Api.Admin)
            ?? CheckPages(PluginArea.Client, manifest.Ui.Client)
            ?? CheckPages(PluginArea.Admin, manifest.Ui.Admin)
            ?? CheckSlots(manifest.Ui.Slots)
            ?? CheckTextKeys(manifest);
    }

    /// <summary>
    /// Checks the assemblies in a plugin folder: one built against an older SDK major version than the host's is
    /// refused up front, because loading it would fail with a type load exception that names nothing useful. The
    /// assembly metadata is read without loading it, so the plugin never reaches the default load context.
    /// </summary>
    /// <param name="pluginDirectory">The folder holding the plugin's assemblies.</param>
    /// <returns>The violation naming the first outdated or unreadable assembly, or <see langword="null"/> when none is.</returns>
    internal static PluginManifestViolation? ValidateSdkReference(string pluginDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);

        foreach (var assemblyPath in Directory.EnumerateFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            Version? referenced;
            try
            {
                referenced = ReadSdkReferenceVersion(assemblyPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A file the host cannot read is a refusal of this plugin, not a fault of the request; only the exception type is kept.
                return new PluginManifestViolation(
                    PluginManifestRule.AssemblyUnreadable, $"{Path.GetFileName(assemblyPath)} could not be read ({ex.GetType().Name})");
            }

            if (referenced is not null && referenced.Major < HostSdkMajorVersion)
            {
                return new PluginManifestViolation(
                    PluginManifestRule.SdkAssemblyOutdated,
                    $"{Path.GetFileName(assemblyPath)} was built against SDK {referenced.Major}.{referenced.Minor}, "
                    + $"this host provides SDK {HostSdkMajorVersion}; rebuild the plugin against the current SDK");
            }
        }

        return null;
    }

    /// <summary>Reads the version of the SDK that a managed assembly references, from its metadata only.</summary>
    /// <param name="assemblyPath">The assembly file.</param>
    /// <returns>The referenced SDK version, or <see langword="null"/> when the file is not a managed assembly or does not reference the SDK.</returns>
    /// <exception cref="IOException">The file cannot be opened or read.</exception>
    /// <exception cref="UnauthorizedAccessException">The host may not read the file.</exception>
    private static Version? ReadSdkReferenceVersion(string assemblyPath)
    {
        try
        {
            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
            {
                return null;
            }

            var metadata = peReader.GetMetadataReader();
            foreach (var handle in metadata.AssemblyReferences)
            {
                var reference = metadata.GetAssemblyReference(handle);
                if (string.Equals(metadata.GetString(reference.Name), SdkAssemblyName, StringComparison.Ordinal))
                {
                    return reference.Version;
                }
            }

            return null;
        }
        catch (BadImageFormatException)
        {
            // A native library or a damaged file: it cannot have been built against the SDK, and loading it fails on its own terms.
            return null;
        }
    }

    /// <summary>Checks the routes one area declares: permission, path and uniqueness.</summary>
    /// <param name="area">The panel the routes belong to.</param>
    /// <param name="routes">The routes of the area.</param>
    /// <returns>The first violation, or <see langword="null"/>.</returns>
    private static PluginManifestViolation? CheckRoutes(PluginArea area, IReadOnlyList<PluginRoute> routes)
    {
        var seen = new HashSet<(PluginRouteMethod Method, string Template)>();
        foreach (var route in routes)
        {
            var where = $"api.{AreaMember(area)} {route.Method} '{route.Path}'";
            var violation = CheckPermission(area, route.Permission, where);
            if (violation is not null)
            {
                return violation;
            }

            if (!TryNormalizePath(route.Path, out var template))
            {
                return PathViolation(where);
            }

            if (!seen.Add((route.Method, template)))
            {
                return new PluginManifestViolation(
                    PluginManifestRule.RouteDuplicate, $"{where} is declared more than once in the {AreaMember(area)} area");
            }
        }

        return null;
    }

    /// <summary>The violation for a path that breaks the path rule.</summary>
    /// <param name="where">Where the path is declared, for the log.</param>
    /// <returns>The <see cref="PluginManifestRule.RoutePathInvalid"/> violation.</returns>
    private static PluginManifestViolation PathViolation(string where)
    {
        return new PluginManifestViolation(
            PluginManifestRule.RoutePathInvalid,
            $"{where}: a path is lowercase segments and {{parameters}} below a leading slash, with no empty segment and no '..'");
    }

    /// <summary>Checks the pages of one area: a permission the area does not take or does not know, and the path rule of routes.</summary>
    /// <param name="area">The panel the pages belong to.</param>
    /// <param name="ui">The menus and pages of the area.</param>
    /// <returns>The first violation, or <see langword="null"/>.</returns>
    private static PluginManifestViolation? CheckPages(PluginArea area, PluginUiArea ui)
    {
        foreach (var page in ui.Pages)
        {
            var where = $"ui.{AreaMember(area)}.pages '{page.Id}' path '{page.Path}'";
            var violation = CheckPermission(area, page.Permission, where);
            if (violation is not null)
            {
                return violation;
            }

            // A page path sits below the plugin's own prefix like a route path does, so the same rule holds.
            if (!TryNormalizePath(page.Path, out _))
            {
                return PathViolation(where);
            }
        }

        return null;
    }

    /// <summary>Checks the slot entries: the slot exists, offers each context key, and its permission fits its area.</summary>
    /// <param name="slots">The slot entries.</param>
    /// <returns>The first violation, or <see langword="null"/>.</returns>
    private static PluginManifestViolation? CheckSlots(IReadOnlyList<PluginSlotEntry> slots)
    {
        foreach (var entry in slots)
        {
            var where = $"ui.slots '{entry.Id}'";
            var slot = PluginSlots.Find(entry.Slot);
            if (slot is null)
            {
                return new PluginManifestViolation(PluginManifestRule.SlotUnknown, $"{where}: the host offers no slot '{entry.Slot}'");
            }

            var missing = entry.ContextKeys.FirstOrDefault(key => !slot.Offers(key));
            if (missing is not null)
            {
                return new PluginManifestViolation(
                    PluginManifestRule.SlotContextKeyNotOffered, $"{where}: slot '{slot.Id}' does not offer the context key '{missing}'");
            }

            var violation = CheckPermission(slot.Area, entry.Permission, where);
            if (violation is not null)
            {
                return violation;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks a permission against the area it is declared in: an admin entry takes none, a client entry names
    /// <see cref="PluginPermissions.Any"/> or a defined client permission.
    /// </summary>
    /// <param name="area">The panel of the entry.</param>
    /// <param name="permission">The declared permission, or <see langword="null"/> for none.</param>
    /// <param name="where">Where the entry is declared, for the log.</param>
    /// <returns>The violation, or <see langword="null"/>.</returns>
    private static PluginManifestViolation? CheckPermission(PluginArea area, string? permission, string where)
    {
        if (permission is null)
        {
            return null;
        }

        if (area == PluginArea.Admin)
        {
            return new PluginManifestViolation(
                PluginManifestRule.AdminPermissionDeclared, $"{where} declares permission '{permission}' and the admin area takes none");
        }

        if (permission == PluginPermissions.Any || ClientPermissionNames.Contains(permission))
        {
            return null;
        }

        return new PluginManifestViolation(
            PluginManifestRule.PermissionUnknown,
            $"{where} names permission '{permission}', which is neither '{PluginPermissions.Any}' nor a client permission");
    }

    /// <summary>
    /// Checks that every text key the menus, pages, settings and slots use is defined in the English texts. Two kinds
    /// of member are told apart. A menu or setting <c>label</c> and a page or slot <c>title</c> is a key when its value is
    /// shaped like one (see <see cref="TextKeyPattern"/>); any other value is plain text and needs no entry. A setting
    /// <c>description</c> and <c>placeholder</c> is usually literal help text or an example such as
    /// <c>api.example.org</c>, which has the shape of a key, so it counts as a key only when some locale defines that
    /// exact value; it must then be defined in English too.
    /// </summary>
    /// <param name="manifest">The manifest.</param>
    /// <returns>The first violation, or <see langword="null"/>.</returns>
    private static PluginManifestViolation? CheckTextKeys(PluginManifest manifest)
    {
        var english = manifest.Locales
            .FirstOrDefault(pair => string.Equals(pair.Key, EnglishLocale, StringComparison.OrdinalIgnoreCase)).Value;

        foreach (var (where, value, isHelpText) in TextValues(manifest))
        {
            if (value is null)
            {
                continue;
            }

            var isKey = isHelpText
                ? manifest.Locales.Values.Any(texts => texts.ContainsKey(value))
                : TextKeyPattern().IsMatch(value);
            if (isKey && (english is null || !english.ContainsKey(value)))
            {
                return new PluginManifestViolation(
                    PluginManifestRule.LabelKeyMissing, $"{where} uses the text key '{value}', which the '{EnglishLocale}' texts do not define");
            }
        }

        return null;
    }

    /// <summary>Lists every text member a manifest can fill with a key: menu labels, page and slot titles, setting texts.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <returns>
    /// Each member's location for the log, its value (which may be <see langword="null"/>), and whether it is help text
    /// (a setting description or placeholder) that is a key only when a locale defines it.
    /// </returns>
    private static IEnumerable<(string Where, string? Value, bool IsHelpText)> TextValues(PluginManifest manifest)
    {
        (PluginArea Area, PluginUiArea Ui)[] areas = [(PluginArea.Client, manifest.Ui.Client), (PluginArea.Admin, manifest.Ui.Admin)];
        foreach (var (area, ui) in areas)
        {
            foreach (var menu in ui.Menus)
            {
                yield return ($"ui.{AreaMember(area)}.menus '{menu.Id}' label", menu.Label, false);
            }

            foreach (var page in ui.Pages)
            {
                yield return ($"ui.{AreaMember(area)}.pages '{page.Id}' title", page.Title, false);
            }
        }

        foreach (var entry in manifest.Ui.Slots)
        {
            yield return ($"ui.slots '{entry.Id}' title", entry.Title, false);
        }

        foreach (var setting in manifest.Settings)
        {
            yield return ($"settings '{setting.Key}' label", setting.Label, false);
            yield return ($"settings '{setting.Key}' description", setting.Description, true);
            yield return ($"settings '{setting.Key}' placeholder", setting.Placeholder, true);
        }
    }

    /// <summary>
    /// Splits a route path into segments and checks each one, returning the template with every parameter name
    /// replaced by a placeholder, so <c>/a/{id}</c> and <c>/a/{name}</c> count as the same route.
    /// </summary>
    /// <param name="path">The declared path.</param>
    /// <param name="template">The normalised template, when the path is valid.</param>
    /// <returns><see langword="true"/> when the path starts with a slash and every segment is a lowercase literal or a parameter.</returns>
    private static bool TryNormalizePath(string? path, out string template)
    {
        template = string.Empty;
        if (string.IsNullOrEmpty(path) || path[0] != '/')
        {
            return false;
        }

        var normalized = new List<string>();
        foreach (var segment in path[1..].Split('/'))
        {
            if (ParameterSegmentPattern().IsMatch(segment))
            {
                normalized.Add("{}");
            }
            else if (LiteralSegmentPattern().IsMatch(segment))
            {
                normalized.Add(segment);
            }
            else
            {
                // Empty, "." or "..", uppercase, or any other character.
                return false;
            }
        }

        template = "/" + string.Join('/', normalized);
        return true;
    }

    /// <summary>The name of an area's member in <c>api</c> and <c>ui</c>, as the manifest spells it.</summary>
    /// <param name="area">The panel.</param>
    /// <returns><c>client</c> or <c>admin</c>.</returns>
    private static string AreaMember(PluginArea area)
    {
        return area == PluginArea.Admin ? "admin" : "client";
    }
}
