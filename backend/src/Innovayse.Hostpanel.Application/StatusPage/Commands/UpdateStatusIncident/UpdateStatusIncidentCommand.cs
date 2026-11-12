namespace Innovayse.Hostpanel.Application.StatusPage.Commands.UpdateStatusIncident;

/// <summary>Command to change the headline of an incident.</summary>
/// <param name="Id">
/// The incident identifier; wins over any id in the body. A value type, so it is left non-nullable: a missing
/// <c>int</c> in the body binds to <c>0</c> and the route overlay (<c>command with { Id = id }</c>) replaces it.
/// </param>
/// <param name="Title">The new headline visitors see.</param>
public sealed record UpdateStatusIncidentCommand(int Id, string Title);
