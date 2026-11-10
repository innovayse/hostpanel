namespace Innovayse.Hostpanel.Application.Clients.Commands.SetDefaultClientGroup;

/// <summary>Command to make a client group the default for new clients, or to stop it being the default.</summary>
/// <param name="Id">The group primary key. Overlaid from the route.</param>
/// <param name="IsDefault">True to make it the one default group, false to leave it out of the default role.</param>
public sealed record SetDefaultClientGroupCommand(int Id, bool IsDefault);
