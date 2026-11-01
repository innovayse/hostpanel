namespace Innovayse.Hostpanel.Integration.Tests.Fixtures.Enums;

/// <summary>The kinds of flawed package the plugin signature tests build.</summary>
public enum PluginPackageFlaw
{
    /// <summary>No <c>plugin.sig</c>.</summary>
    Unsigned,

    /// <summary>Signed by the trusted key, then a file changed.</summary>
    Tampered,

    /// <summary>Signed by a key the verifier does not know.</summary>
    UnknownKey,
}
