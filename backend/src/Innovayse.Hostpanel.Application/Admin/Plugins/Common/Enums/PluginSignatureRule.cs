namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

/// <summary>
/// A way a plugin package fails the signature check that follows the manifest rules. Like
/// <see cref="PluginManifestRule"/>, the same value names the logged reason when a plugin is skipped at
/// startup and picks the error code when an upload is refused. Values start at 1; 0 is not a rule.
/// </summary>
public enum PluginSignatureRule
{
    /// <summary>The package carries no <c>plugin.sig</c>.</summary>
    Unsigned = 1,

    /// <summary>
    /// <c>plugin.sig</c> cannot be read as a signature (not JSON, wrong shape, a signature that is not 64 bytes),
    /// or the package holds a file name that cannot be signed unambiguously.
    /// </summary>
    Malformed = 2,

    /// <summary>The signature names a key id that is not among the trusted keys.</summary>
    UnknownKey = 3,

    /// <summary>The signature does not match the package's files: a file was changed, added or removed after signing.</summary>
    Invalid = 4,
}
