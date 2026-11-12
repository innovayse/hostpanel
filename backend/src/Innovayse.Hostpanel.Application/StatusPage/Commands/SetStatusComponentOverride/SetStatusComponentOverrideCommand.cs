using Innovayse.Hostpanel.Domain.StatusPage.Enums;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.SetStatusComponentOverride;

/// <summary>Command to force a state onto the status page for one component, or to hand the state back to the automatic checks.</summary>
/// <param name="Id">
/// The component identifier; wins over any id in the body. A value type, so it is left non-nullable: a missing
/// <c>int</c> in the body binds to <c>0</c> and the route overlay (<c>command with { Id = id }</c>) replaces it.
/// </param>
/// <param name="State">The state to show whatever the checks find, or <c>null</c> to clear the override.</param>
public sealed record SetStatusComponentOverrideCommand(int Id, StatusComponentState? State);
