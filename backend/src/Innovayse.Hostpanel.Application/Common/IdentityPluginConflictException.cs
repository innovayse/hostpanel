namespace Innovayse.Hostpanel.Application.Common;

/// <summary>
/// Thrown by <see cref="Interfaces.IIdentityPluginResolver"/> when more than one identity plugin is enabled.
/// At most one plugin may decide which client a principal acts for: two would each name a different client
/// for the same person, and choosing one of them silently would let a configuration mistake move a person
/// onto another account. It is a fault of the deployment's configuration, not a refusal a caller can
/// answer, so it is an exception and the request fails until an admin disables all but one.
/// </summary>
public sealed class IdentityPluginConflictException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="IdentityPluginConflictException"/> class.</summary>
    /// <param name="pluginIds">The ids of every enabled identity plugin, so the log line names what to disable.</param>
    public IdentityPluginConflictException(IReadOnlyList<string> pluginIds)
        : base($"More than one identity plugin is enabled ({string.Join(", ", pluginIds)}); enable at most one.")
    {
        PluginIds = pluginIds;
    }

    /// <summary>Gets the ids of every enabled identity plugin, in plugin id order.</summary>
    public IReadOnlyList<string> PluginIds { get; }
}
