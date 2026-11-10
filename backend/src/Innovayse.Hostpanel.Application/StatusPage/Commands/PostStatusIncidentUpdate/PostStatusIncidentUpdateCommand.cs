using Innovayse.Hostpanel.Domain.StatusPage.Enums;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.PostStatusIncidentUpdate;

/// <summary>Command to add a timeline entry to an incident and move it to a state; moving it to resolved closes it.</summary>
/// <param name="Id">
/// The incident identifier; wins over any id in the body. A value type, so it is left non-nullable: a missing
/// <c>int</c> in the body binds to <c>0</c> and the route overlay (<c>command with { Id = id }</c>) replaces it.
/// </param>
/// <param name="State">The state the incident moves to.</param>
/// <param name="Message">What the visitors are told.</param>
public sealed record PostStatusIncidentUpdateCommand(int Id, StatusIncidentState State, string Message);
