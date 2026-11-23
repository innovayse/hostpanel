namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The SSH key agent installer bundles this HostPanel build can hand out: the one with the agent and its built in profiles,
/// and, for each profile a loaded plugin ships, the same files plus that plugin's profile and helpers. A file a plugin ships
/// wins over a built in file at the same path, so a plugin's profile replaces a built in one with the same id; the built in
/// default profile is the host's own and no plugin can take it.
/// </summary>
public interface ISshAgentBundles
{
    /// <summary>Gets the bundle that carries the agent and its built in profile only.</summary>
    public ISshAgentBundle BuiltIn { get; }

    /// <summary>Finds the bundle for a profile id.</summary>
    /// <param name="profile">The profile id: a built in one, or the one a loaded plugin names in its manifest; matched exactly.</param>
    /// <returns>The bundle; <see langword="null"/> when the id is neither built in nor shipped by a loaded plugin.</returns>
    public ISshAgentBundle? For(string profile);
}
