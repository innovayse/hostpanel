namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

/// <summary>Where this installation stands with the plugin registry; serialised as the name.</summary>
public enum RegistryInstanceState
{
    /// <summary>No registration has been made yet: nothing was needed, or the first attempt is still to come.</summary>
    NotRegistered = 1,

    /// <summary>Registered, and the registry has not yet confirmed the challenge this installation serves.</summary>
    Pending = 2,

    /// <summary>The registry confirmed the installation; signed requests are accepted.</summary>
    Verified = 3,

    /// <summary>The registry revoked the installation. Only an explicit retry registers it again.</summary>
    Revoked = 4,

    /// <summary>The last attempt failed; the next one waits for the back-off or for a retry.</summary>
    Error = 5,
}
