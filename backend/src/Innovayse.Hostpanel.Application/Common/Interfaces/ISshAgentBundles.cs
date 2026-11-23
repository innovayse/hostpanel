namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The SSH key agent installer bundles this HostPanel build can hand out: the one with the agent and its built in profile,
/// and, for each profile a loaded plugin ships, the same files plus that plugin's profile and helpers. A plugin's profile can
/// never replace the built in one.
/// </summary>
public interface ISshAgentBundles
{
    /// <summary>Gets the bundle that carries the agent and its built in profile only.</summary>
    public ISshAgentBundle BuiltIn { get; }

    /// <summary>Finds the bundle for a profile id.</summary>
    /// <param name="profile">The profile id: the built in one, or the one a loaded plugin names in its manifest; matched exactly.</param>
    /// <returns>The bundle; <see langword="null"/> when no loaded plugin ships a profile with that id.</returns>
    public ISshAgentBundle? For(string profile);
}
