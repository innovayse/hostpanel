using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>Maps a refused <see cref="PluginPackageOutcome"/> to the error code the API answers with.</summary>
public static class PluginPackageErrors
{
    /// <summary>The error code for a refused result.</summary>
    /// <param name="result">A refusal; a success has no code.</param>
    /// <returns>The matching <see cref="HostpanelErrorCodes"/> constant.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The result is a success, or a broken manifest rule is not named.</exception>
    public static string CodeFor(PluginPackageResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Outcome == PluginPackageOutcome.ManifestRuleBroken)
        {
            return CodeFor(result.Rule ?? throw new ArgumentOutOfRangeException(nameof(result), result, "A broken manifest rule must be named."));
        }

        return result.Outcome switch
        {
            PluginPackageOutcome.ManifestInvalid => HostpanelErrorCodes.PLUGIN_MANIFEST_INVALID,
            PluginPackageOutcome.SdkVersionUnsupported => HostpanelErrorCodes.PLUGIN_SDK_VERSION_UNSUPPORTED,
            PluginPackageOutcome.IdInvalid => HostpanelErrorCodes.PLUGIN_ID_INVALID,
            PluginPackageOutcome.ArchiveUnsafe => HostpanelErrorCodes.PLUGIN_ARCHIVE_UNSAFE,
            PluginPackageOutcome.ArchiveTooLarge => HostpanelErrorCodes.PLUGIN_ARCHIVE_TOO_LARGE,
            PluginPackageOutcome.HostAssemblyRefused => HostpanelErrorCodes.PLUGIN_HOST_ASSEMBLY_REFUSED,
            PluginPackageOutcome.NotTrusted => HostpanelErrorCodes.PLUGIN_NOT_TRUSTED,
            PluginPackageOutcome.BuiltIn => HostpanelErrorCodes.PLUGIN_BUILT_IN,
            PluginPackageOutcome.NotFound => HostpanelErrorCodes.PLUGIN_NOT_FOUND,
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, "A success outcome has no error code."),
        };
    }

    /// <summary>The error code for a broken manifest rule.</summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The matching <see cref="HostpanelErrorCodes"/> constant.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The rule is not a defined one.</exception>
    public static string CodeFor(PluginManifestRule rule)
    {
        return rule switch
        {
            PluginManifestRule.ManifestVersionUnknown => HostpanelErrorCodes.PLUGIN_MANIFEST_VERSION_UNKNOWN,
            PluginManifestRule.SdkAssemblyOutdated => HostpanelErrorCodes.PLUGIN_SDK_ASSEMBLY_OUTDATED,
            PluginManifestRule.PermissionUnknown => HostpanelErrorCodes.PLUGIN_PERMISSION_UNKNOWN,
            PluginManifestRule.AdminPermissionDeclared => HostpanelErrorCodes.PLUGIN_ADMIN_PERMISSION_DECLARED,
            PluginManifestRule.RoutePathInvalid => HostpanelErrorCodes.PLUGIN_ROUTE_PATH_INVALID,
            PluginManifestRule.RouteDuplicate => HostpanelErrorCodes.PLUGIN_ROUTE_DUPLICATE,
            PluginManifestRule.LabelKeyMissing => HostpanelErrorCodes.PLUGIN_LABEL_KEY_MISSING,
            PluginManifestRule.SlotUnknown => HostpanelErrorCodes.PLUGIN_SLOT_UNKNOWN,
            PluginManifestRule.SlotContextKeyNotOffered => HostpanelErrorCodes.PLUGIN_SLOT_CONTEXT_KEY_NOT_OFFERED,
            PluginManifestRule.AssemblyUnreadable => HostpanelErrorCodes.PLUGIN_ASSEMBLY_UNREADABLE,
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "Not a manifest rule."),
        };
    }
}
