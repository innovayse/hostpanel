namespace Innovayse.Hostpanel.Application.StatusPage.Commands.UpdateStatusComponentGroup;

/// <summary>Command to rename a status page group.</summary>
/// <param name="Id">
/// The group identifier; wins over any id in the body. A value type, so it is left non-nullable: a missing
/// <c>int</c> in the body binds to <c>0</c> and the route overlay (<c>command with { Id = id }</c>) replaces it.
/// </param>
/// <param name="Name">The display name.</param>
public sealed record UpdateStatusComponentGroupCommand(int Id, string Name);
