using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using PluginSignatureRule = Hostpanel.Sdk.Packaging.Enums.PluginSignatureRule;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>Maps a refused <see cref="PluginPackageOutcome"/> to the error code the API answers with.</summary>
public static class PluginPackageErrors
{
    /// <summary>The error code for a refused result.</summary>
    /// <param name="result">A refusal; a success has no code.</param>
    /// <returns>The matching <see cref="HostpanelErrorCodes"/> constant.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The result is a success, or a broken manifest rule or a refused signature is not named.</exception>
    public static string CodeFor(PluginPackageResult result)
    {
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
            PluginPackageOutcome.ArchiveUnwritable => HostpanelErrorCodes.PLUGIN_ARCHIVE_UNWRITABLE,
            PluginPackageOutcome.ArchiveTooLarge => HostpanelErrorCodes.PLUGIN_ARCHIVE_TOO_LARGE,
            PluginPackageOutcome.HostAssemblyRefused => HostpanelErrorCodes.PLUGIN_HOST_ASSEMBLY_REFUSED,
            PluginPackageOutcome.AssemblyConflict => HostpanelErrorCodes.PLUGIN_ASSEMBLY_CONFLICT,
            PluginPackageOutcome.NotTrusted => HostpanelErrorCodes.PLUGIN_NOT_TRUSTED,
            PluginPackageOutcome.SignatureRefused => CodeFor(
                result.SignatureRule ?? throw new ArgumentOutOfRangeException(nameof(result), result, "A refused signature must be named.")),
            PluginPackageOutcome.DowngradeRefused => HostpanelErrorCodes.PLUGIN_DOWNGRADE_REFUSED,
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
            PluginManifestRule.SlotWidgetIdInvalid => HostpanelErrorCodes.PLUGIN_SLOT_WIDGET_ID_INVALID,
            PluginManifestRule.SlotWidgetDuplicate => HostpanelErrorCodes.PLUGIN_SLOT_WIDGET_DUPLICATE,
            PluginManifestRule.PluginIdInvalid => HostpanelErrorCodes.PLUGIN_MANIFEST_ID_INVALID,
            PluginManifestRule.VersionInvalid => HostpanelErrorCodes.PLUGIN_MANIFEST_VERSION_INVALID,
            PluginManifestRule.ViewMissing => HostpanelErrorCodes.PLUGIN_VIEW_MISSING,
            PluginManifestRule.ViewInvalid => HostpanelErrorCodes.PLUGIN_VIEW_INVALID,
            PluginManifestRule.ViewSchemaUnsupported => HostpanelErrorCodes.PLUGIN_VIEW_SCHEMA_UNSUPPORTED,
            PluginManifestRule.ViewBlockKindUnknown => HostpanelErrorCodes.PLUGIN_VIEW_BLOCK_KIND_UNKNOWN,
            PluginManifestRule.ViewRouteUndeclared => HostpanelErrorCodes.PLUGIN_VIEW_ROUTE_UNDECLARED,
            PluginManifestRule.MenuIconInvalid => HostpanelErrorCodes.PLUGIN_MENU_ICON_INVALID,
            PluginManifestRule.PageIdInvalid => HostpanelErrorCodes.PLUGIN_PAGE_ID_INVALID,
            PluginManifestRule.EndpointsNotDeclared => HostpanelErrorCodes.PLUGIN_ENDPOINTS_NOT_DECLARED,
            PluginManifestRule.CapabilityNotAllowed => HostpanelErrorCodes.PLUGIN_CAPABILITY_NOT_ALLOWED,
            PluginManifestRule.HostAccessUnknown => HostpanelErrorCodes.PLUGIN_HOST_ACCESS_UNKNOWN,
            PluginManifestRule.ViewConditionInvalid => HostpanelErrorCodes.PLUGIN_VIEW_CONDITION_INVALID,
            PluginManifestRule.ViewActionBodyInvalid => HostpanelErrorCodes.PLUGIN_VIEW_ACTION_BODY_INVALID,
            PluginManifestRule.ViewSelectionInvalid => HostpanelErrorCodes.PLUGIN_VIEW_SELECTION_INVALID,
            PluginManifestRule.ViewFollowUpInvalid => HostpanelErrorCodes.PLUGIN_VIEW_FOLLOW_UP_INVALID,
            PluginManifestRule.PageLayoutUnknown => HostpanelErrorCodes.PLUGIN_PAGE_LAYOUT_UNKNOWN,
            PluginManifestRule.ViewSplitInvalid => HostpanelErrorCodes.PLUGIN_VIEW_SPLIT_INVALID,
            PluginManifestRule.ViewPreviewInvalid => HostpanelErrorCodes.PLUGIN_VIEW_PREVIEW_INVALID,
            PluginManifestRule.ViewBlockIdRepeated => HostpanelErrorCodes.PLUGIN_VIEW_BLOCK_ID_REPEATED,
            PluginManifestRule.CallbackInvalid => HostpanelErrorCodes.PLUGIN_CALLBACK_INVALID,
            PluginManifestRule.IconMissing => HostpanelErrorCodes.PLUGIN_ICON_MISSING,
            PluginManifestRule.IconInvalid => HostpanelErrorCodes.PLUGIN_ICON_INVALID,
            PluginManifestRule.EndpointsNotImplemented => HostpanelErrorCodes.PLUGIN_ENDPOINTS_NOT_IMPLEMENTED,
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "Not a manifest rule."),
        };
    }

    /// <summary>The error code for a failed signature check, as the plugin packaging package names it.</summary>
    /// <param name="rule">The check the package failed.</param>
    /// <returns>The matching <see cref="HostpanelErrorCodes"/> constant.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The rule is not a defined one.</exception>
    public static string CodeFor(PluginSignatureRule rule)
    {
        return rule switch
        {
            PluginSignatureRule.Unsigned => HostpanelErrorCodes.PLUGIN_UNSIGNED,
            PluginSignatureRule.Malformed => HostpanelErrorCodes.PLUGIN_SIGNATURE_MALFORMED,
            PluginSignatureRule.UnknownKey => HostpanelErrorCodes.PLUGIN_SIGNATURE_KEY_UNKNOWN,
            PluginSignatureRule.Invalid => HostpanelErrorCodes.PLUGIN_SIGNATURE_INVALID,
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "Not a signature rule."),
        };
    }
}
