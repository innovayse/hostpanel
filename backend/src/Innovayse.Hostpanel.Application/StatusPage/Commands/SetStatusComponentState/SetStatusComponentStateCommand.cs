using Innovayse.Hostpanel.Domain.StatusPage.Enums;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.SetStatusComponentState;

/// <summary>Command to set the state the status page shows for one component.</summary>
/// <param name="Id">
/// The component identifier; wins over any id in the body. A value type, so it is left non-nullable: a missing
/// <c>int</c> in the body binds to <c>0</c> and the route overlay (<c>command with { Id = id }</c>) replaces it.
/// </param>
/// <param name="State">The new state.</param>
public sealed record SetStatusComponentStateCommand(int Id, StatusComponentState State);
