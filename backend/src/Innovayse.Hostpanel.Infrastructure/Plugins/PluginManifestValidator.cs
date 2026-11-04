using Hostpanel.Sdk.Packaging.Manifest;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Host;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;
using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The one place the host checks a plugin manifest and the assemblies beside it against the manifest rules.
/// Loading a plugin at startup (<see cref="PluginManifestReader.TryRead(string, out PluginManifestViolation?)"/>) and installing an upload
/// (<see cref="PluginPackageStore"/>) both ask it, so a rule is written once and holds on both paths.
/// </summary>
/// <remarks>
/// The structural rules (versions, ids, permissions, route and page paths, slots, menu icons, text keys) are the
/// plugin packaging package's <see cref="PluginManifestChecker"/>, the same code a plugin author runs before
/// publishing. What the host adds is what only it knows: the permission names its clients can hold and the manifest
/// versions it reads. The package reports its own rule enum; <see cref="PluginRuleMap"/> turns it into the host's.
/// </remarks>
internal static class PluginManifestValidator
{
    /// <summary>The first manifest version that may declare <c>hostAccess</c>.</summary>
    private const int HostAccessManifestVersion = 2;

    /// <summary>
    /// The capabilities this host knows and the plugin types that may carry each; <see langword="null"/> means every
    /// type. A name missing here is refused, so a capability the host cannot honour never reaches activation.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, PluginType?> CapabilityTypes =
        new Dictionary<string, PluginType?>(StringComparer.Ordinal)
        {
            [PluginCapabilities.StoredCredentials] = PluginType.Payment,
            [PluginCapabilities.Endpoints] = null,
        };

    /// <summary>
    /// What the host decides for the package's rules: the manifest versions it reads (the package's default, <c>1</c> and
    /// <c>2</c>) and the permission names a client route, page or slot entry may name, every defined
    /// <see cref="ClientPermission"/> except <see cref="ClientPermission.None"/> (a permission that requires nothing is
    /// written <see cref="PluginPermissions.Any"/>), compared exactly.
    /// </summary>
    private static readonly PluginManifestOptions Options = new()
    {
        ClientPermissionNames = Enum.GetNames<ClientPermission>()
            .Where(name => name != nameof(ClientPermission.None))
            .ToHashSet(StringComparer.Ordinal),
    };

    /// <summary>
    /// Checks the manifest alone: its version, permissions, route paths, text keys and slots. A version 1 manifest
    /// declares none of the blocks these rules look at, so it passes by having nothing to break.
    /// </summary>
    /// <param name="manifest">The manifest, already read.</param>
    /// <returns>The first rule the manifest breaks, or <see langword="null"/> when it meets them all.</returns>
    internal static PluginManifestViolation? Validate(PluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var violation = PluginManifestChecker.Validate(manifest, Options);
        if (violation is not null)
        {
            return PluginRuleMap.ToHost(violation);
        }

        // What follows is what only the host knows: the access kinds and capabilities it defines and whether it serves
        // the plugin's endpoints. The package knows none of these.
        return CheckHostAccess(manifest)
            ?? CheckCapabilities(manifest)
            ?? CheckEndpointsDeclared(manifest);
    }

    /// <summary>
    /// Checks that every declared capability is one the host knows, declared once, and one the plugin's type may carry.
    /// </summary>
    /// <param name="manifest">The manifest.</param>
    /// <returns>The <see cref="PluginManifestRule.CapabilityNotAllowed"/> violation, or <see langword="null"/>.</returns>
    private static PluginManifestViolation? CheckCapabilities(PluginManifest manifest)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var capability in manifest.Capabilities)
        {
            // A repeat is refused rather than ignored: it is a manifest mistake, and the list is shown to the operator.
            if (!seen.Add(capability))
            {
                return new PluginManifestViolation(
                    PluginManifestRule.CapabilityNotAllowed, $"capabilities names '{capability}' twice");
            }

            if (!CapabilityTypes.TryGetValue(capability, out var only))
            {
                return new PluginManifestViolation(
                    PluginManifestRule.CapabilityNotAllowed, $"capabilities names '{capability}', which this host does not define");
            }

            if (only is { } type && manifest.Type != type)
            {
                return new PluginManifestViolation(
                    PluginManifestRule.CapabilityNotAllowed, $"capabilities names '{capability}', which only a {type} plugin may carry");
            }
        }

        return null;
    }

    /// <summary>
    /// Refuses <c>api</c> routes and <c>ui</c> menus, pages and slots on a plugin the host would not serve: they would be
    /// accepted and then silently never answer, which reads as a host fault to the plugin author.
    /// </summary>
    /// <param name="manifest">The manifest.</param>
    /// <returns>The <see cref="PluginManifestRule.EndpointsNotDeclared"/> violation, or <see langword="null"/>.</returns>
    private static PluginManifestViolation? CheckEndpointsDeclared(PluginManifest manifest)
    {
        if (manifest.ServesEndpoints())
        {
            return null;
        }

        var declaresAny = manifest.Api.Client.Count > 0
            || manifest.Api.Admin.Count > 0
            || manifest.Ui.Client.Menus.Count > 0
            || manifest.Ui.Client.Pages.Count > 0
            || manifest.Ui.Admin.Menus.Count > 0
            || manifest.Ui.Admin.Pages.Count > 0
            || manifest.Ui.Slots.Count > 0;
        return declaresAny
            ? new PluginManifestViolation(
                PluginManifestRule.EndpointsNotDeclared,
                $"a {manifest.Type} plugin declares api or ui entries without the '{PluginCapabilities.Endpoints}' capability")
            : null;
    }

    /// <summary>
    /// Checks <c>hostAccess</c>: every entry is a kind the SDK defines (<see cref="PluginHostAccess.All"/>, exact case),
    /// none twice, and only in a version 2 manifest. An unknown kind would otherwise be shown to the admin as a capability
    /// the host never grants, and a version 1 manifest predates the member.
    /// </summary>
    /// <param name="manifest">The manifest.</param>
    /// <returns>The <see cref="PluginManifestRule.HostAccessUnknown"/> violation, or <see langword="null"/>.</returns>
    private static PluginManifestViolation? CheckHostAccess(PluginManifest manifest)
    {
        if (manifest.HostAccess.Count == 0)
        {
            return null;
        }

        if (manifest.ManifestVersion < HostAccessManifestVersion)
        {
            return new PluginManifestViolation(
                PluginManifestRule.HostAccessUnknown, "hostAccess is declared in a version 1 manifest");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kind in manifest.HostAccess)
        {
            if (kind is null || !PluginHostAccess.All.Contains(kind, StringComparer.Ordinal) || !seen.Add(kind))
            {
                return new PluginManifestViolation(
                    PluginManifestRule.HostAccessUnknown, $"hostAccess '{kind}' is unknown or listed more than once");
            }
        }

        return null;
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
        var violation = PluginManifestChecker.ValidateSdkReference(pluginDirectory);
        return violation is null ? null : PluginRuleMap.ToHost(violation);
    }

    /// <summary>Gets the English texts of a manifest, the locale that must define every text key.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <returns>The texts by key, or <see langword="null"/> when the manifest ships no English locale.</returns>
    internal static IReadOnlyDictionary<string, string>? EnglishTexts(PluginManifest manifest)
    {
        return PluginManifestChecker.EnglishTexts(manifest);
    }

    /// <summary>Tells whether a label value is shaped like a text key rather than plain text.</summary>
    /// <param name="value">The label value.</param>
    /// <returns><see langword="true"/> when the value has the shape of a text key.</returns>
    internal static bool IsTextKey(string value)
    {
        return PluginManifestChecker.IsTextKey(value);
    }

    /// <summary>Checks that a value taken as a text key is defined in the English texts; the one rule manifest and view labels share.</summary>
    /// <param name="where">Where the value is written, for the log.</param>
    /// <param name="key">The text key.</param>
    /// <param name="english">The English texts, or <see langword="null"/> when there are none.</param>
    /// <returns>The violation, or <see langword="null"/> when the key is defined.</returns>
    internal static PluginManifestViolation? CheckKeyDefined(string where, string key, IReadOnlyDictionary<string, string>? english)
    {
        var violation = PluginManifestChecker.CheckKeyDefined(where, key, english);
        return violation is null ? null : PluginRuleMap.ToHost(violation);
    }
}
