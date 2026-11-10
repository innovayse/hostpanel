namespace Innovayse.Hostpanel.Application.Clients.Commands.DeleteClientGroup;

/// <summary>Command to delete a client group that has no clients.</summary>
/// <param name="Id">The identifier of the group to delete.</param>
public sealed record DeleteClientGroupCommand(int Id);
