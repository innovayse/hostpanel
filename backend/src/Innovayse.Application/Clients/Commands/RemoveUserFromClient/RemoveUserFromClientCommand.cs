namespace Innovayse.Application.Clients.Commands.RemoveUserFromClient;

/// <summary>Command to remove a non-owner user from a client account.</summary>
/// <param name="ClientId">The client account primary key.</param>
/// <param name="UserId">The Identity user ID to remove.</param>
public record RemoveUserFromClientCommand(int ClientId, string UserId);
