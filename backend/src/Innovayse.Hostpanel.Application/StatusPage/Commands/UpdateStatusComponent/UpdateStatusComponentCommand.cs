namespace Innovayse.Hostpanel.Application.StatusPage.Commands.UpdateStatusComponent;

/// <summary>Command to change what a status page component is called and what it covers.</summary>
/// <param name="Id">
/// The component identifier; wins over any id in the body. A value type, so it is left non-nullable: a missing
/// <c>int</c> in the body binds to <c>0</c> and the route overlay (<c>command with { Id = id }</c>) replaces it.
/// </param>
/// <param name="Name">The display name.</param>
/// <param name="Description">A short explanation of what the component covers, or <c>null</c>.</param>
public sealed record UpdateStatusComponentCommand(int Id, string Name, string? Description);
