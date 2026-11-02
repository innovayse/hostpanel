namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

/// <summary>
/// A rule a plugin manifest (and the assemblies beside it) must meet before the host loads or installs the plugin.
/// The same rule names the logged reason when a plugin is skipped at startup and picks the error code when an
/// upload is refused. Values start at 1; 0 is not a rule.
/// </summary>
public enum PluginManifestRule
{
    /// <summary>The <c>manifestVersion</c> is not one this host reads.</summary>
    ManifestVersionUnknown = 1,

    /// <summary>The plugin assembly was built against an older SDK major version than the host's.</summary>
    SdkAssemblyOutdated = 2,

    /// <summary>A client route, page or slot entry names a permission that is neither a client permission nor <c>Any</c>.</summary>
    PermissionUnknown = 3,

    /// <summary>An admin route, page or slot entry declares a permission, and the admin area takes none.</summary>
    AdminPermissionDeclared = 4,

    /// <summary>A route path is not lowercase segments and parameters, or has an empty or dot segment.</summary>
    RoutePathInvalid = 5,

    /// <summary>The same method and path template is declared twice in one area.</summary>
    RouteDuplicate = 6,

    /// <summary>A text key used by a menu, page, setting or slot is missing from the English texts.</summary>
    LabelKeyMissing = 7,

    /// <summary>A slot entry names a slot the host does not offer.</summary>
    SlotUnknown = 8,

    /// <summary>A slot entry asks for a context key its slot does not offer.</summary>
    SlotContextKeyNotOffered = 9,

    /// <summary>A file in the plugin folder could not be read, so its SDK reference could not be checked.</summary>
    AssemblyUnreadable = 10,
}
