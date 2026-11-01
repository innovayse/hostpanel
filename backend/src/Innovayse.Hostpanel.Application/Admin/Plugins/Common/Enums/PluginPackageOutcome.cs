namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

/// <summary>How an install or removal of a plugin package ended.</summary>
public enum PluginPackageOutcome
{
    /// <summary>The package was installed and replaces any earlier upload with the same id.</summary>
    Installed,

    /// <summary>The installed package was removed.</summary>
    Removed,

    /// <summary>The upload is empty, not a readable archive, or its <c>plugin.json</c> is missing or malformed.</summary>
    ManifestInvalid,

    /// <summary>The plugin targets an SDK version this host does not support.</summary>
    SdkVersionUnsupported,

    /// <summary>
    /// The manifest breaks one of the manifest rules (see <see cref="PluginManifestRule"/>), which the result names,
    /// or the plugin was built against an older SDK major version than this host's.
    /// </summary>
    ManifestRuleBroken,

    /// <summary>The plugin id does not meet the plugin id rule or would resolve outside the plugins folder.</summary>
    IdInvalid,

    /// <summary>An archive entry would write outside the plugin's own folder.</summary>
    ArchiveUnsafe,

    /// <summary>The archive has more entries, or unpacks to more bytes, than the configured limits allow.</summary>
    ArchiveTooLarge,

    /// <summary>The archive carries an assembly whose name belongs to the host or the framework.</summary>
    HostAssemblyRefused,

    /// <summary>A trusted-package list is configured and the archive's SHA-256 is not on it.</summary>
    NotTrusted,

    /// <summary>The id belongs to a plugin shipped with the build, which cannot be replaced or removed.</summary>
    BuiltIn,

    /// <summary>No uploaded plugin with that id exists.</summary>
    NotFound,
}
