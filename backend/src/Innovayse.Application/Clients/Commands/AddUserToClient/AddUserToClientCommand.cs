namespace Innovayse.Application.Clients.Commands.AddUserToClient;

/// <summary>Command to add a non-owner user to a client account with specified permissions.</summary>
/// <param name="ClientId">The client account primary key.</param>
/// <param name="UserId">The Identity user ID to link.</param>
/// <param name="Permissions">Granted permissions as a bit-flags integer.</param>
public record AddUserToClientCommand(int ClientId, string UserId, int Permissions);
