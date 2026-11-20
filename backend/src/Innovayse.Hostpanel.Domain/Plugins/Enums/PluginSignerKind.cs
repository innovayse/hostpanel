namespace Innovayse.Hostpanel.Domain.Plugins.Enums;

/// <summary>Who vouched for a plugin package with a detached signature. Stored as the name.</summary>
public enum PluginSignerKind
{
    /// <summary>The package carries no valid signature by a trusted key; it was admitted by the host's policy.</summary>
    Unsigned = 1,

    /// <summary>The package is signed by one of the maintainers' keys shipped with the host: a verified publisher.</summary>
    VerifiedPublisher = 2,

    /// <summary>The package is signed by a key the operator added to <c>PluginSigning:OperatorKeys</c>.</summary>
    OwnKey = 3,
}
