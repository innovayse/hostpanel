using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using PackageArchiveRule = Hostpanel.Sdk.Packaging.Enums.PluginArchiveRule;
using PackageIconRule = Hostpanel.Sdk.Packaging.Enums.PluginIconRule;
using PackageIconViolation = Hostpanel.Sdk.Packaging.Icons.PluginIconViolation;
using PackageManifestRule = Hostpanel.Sdk.Packaging.Enums.PluginManifestRule;
using PackageManifestViolation = Hostpanel.Sdk.Packaging.Manifest.PluginManifestViolation;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The one place the host turns what the plugin packaging package reports into its own vocabulary. The package names
/// each failed check with an enum of its own; the host's <see cref="PluginManifestRule"/> and
/// <see cref="PluginPackageOutcome"/> are supersets (they also hold the rules only the host can check, such as the
/// view document rules) and pick the error code of an install. Every arm maps by name, never by a cast of the number,
/// and an unknown value throws, so a rule added to the package cannot be dropped silently: the mapping tests name
/// every value.
/// </summary>
internal static class PluginRuleMap
{
    /// <summary>Maps a manifest check of the package to the host's rule of the same name.</summary>
    /// <param name="rule">The package's rule.</param>
    /// <returns>The host rule.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The package defines a rule the host has no arm for.</exception>
    public static PluginManifestRule ToHost(PackageManifestRule rule)
    {
        return rule switch
        {
            PackageManifestRule.ManifestVersionUnknown => PluginManifestRule.ManifestVersionUnknown,
            PackageManifestRule.PluginIdInvalid => PluginManifestRule.PluginIdInvalid,
            PackageManifestRule.VersionInvalid => PluginManifestRule.VersionInvalid,
            PackageManifestRule.PermissionUnknown => PluginManifestRule.PermissionUnknown,
            PackageManifestRule.AdminPermissionDeclared => PluginManifestRule.AdminPermissionDeclared,
            PackageManifestRule.RoutePathInvalid => PluginManifestRule.RoutePathInvalid,
            PackageManifestRule.RouteDuplicate => PluginManifestRule.RouteDuplicate,
            PackageManifestRule.CallbackInvalid => PluginManifestRule.CallbackInvalid,
            PackageManifestRule.PageIdInvalid => PluginManifestRule.PageIdInvalid,
            PackageManifestRule.MenuIconInvalid => PluginManifestRule.MenuIconInvalid,
            PackageManifestRule.SlotWidgetIdInvalid => PluginManifestRule.SlotWidgetIdInvalid,
            PackageManifestRule.SlotWidgetDuplicate => PluginManifestRule.SlotWidgetDuplicate,
            PackageManifestRule.SlotUnknown => PluginManifestRule.SlotUnknown,
            PackageManifestRule.SlotContextKeyNotOffered => PluginManifestRule.SlotContextKeyNotOffered,
            PackageManifestRule.LabelKeyMissing => PluginManifestRule.LabelKeyMissing,
            PackageManifestRule.SdkAssemblyOutdated => PluginManifestRule.SdkAssemblyOutdated,
            PackageManifestRule.AssemblyUnreadable => PluginManifestRule.AssemblyUnreadable,
            PackageManifestRule.TagsInvalid => PluginManifestRule.TagsInvalid,
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "The host has no manifest rule for this package rule."),
        };
    }

    /// <summary>Maps an icon check of the package to the host's manifest rule: the host reports an icon as part of the manifest.</summary>
    /// <param name="rule">The package's icon rule.</param>
    /// <returns><see cref="PluginManifestRule.IconMissing"/> or <see cref="PluginManifestRule.IconInvalid"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The package defines an icon rule the host has no arm for.</exception>
    public static PluginManifestRule ToHost(PackageIconRule rule)
    {
        return rule switch
        {
            PackageIconRule.Missing => PluginManifestRule.IconMissing,
            PackageIconRule.Invalid => PluginManifestRule.IconInvalid,
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "The host has no manifest rule for this icon rule."),
        };
    }

    /// <summary>Maps an archive guard of the package to the outcome of an install it refuses with.</summary>
    /// <param name="rule">The package's archive rule.</param>
    /// <returns>The refusing outcome.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The package defines an archive rule the host has no arm for.</exception>
    public static PluginPackageOutcome ToHost(PackageArchiveRule rule)
    {
        return rule switch
        {
            PackageArchiveRule.TooManyEntries => PluginPackageOutcome.ArchiveTooLarge,
            PackageArchiveRule.TooLarge => PluginPackageOutcome.ArchiveTooLarge,
            PackageArchiveRule.EntryOutsideFolder => PluginPackageOutcome.ArchiveUnsafe,
            PackageArchiveRule.EntryNotWritable => PluginPackageOutcome.ArchiveUnwritable,
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "The host has no outcome for this archive rule."),
        };
    }

    /// <summary>Maps a manifest violation of the package to the host's violation.</summary>
    /// <param name="violation">The package's violation.</param>
    /// <returns>The host violation, with the same detail.</returns>
    public static PluginManifestViolation ToHost(PackageManifestViolation violation)
    {
        ArgumentNullException.ThrowIfNull(violation);

        return new PluginManifestViolation(ToHost(violation.Rule), violation.Detail);
    }

    /// <summary>Maps an icon violation of the package to the host's manifest violation.</summary>
    /// <param name="violation">The package's violation.</param>
    /// <returns>The host violation, with the same detail.</returns>
    public static PluginManifestViolation ToHost(PackageIconViolation violation)
    {
        ArgumentNullException.ThrowIfNull(violation);

        return new PluginManifestViolation(ToHost(violation.Rule), violation.Detail);
    }
}
