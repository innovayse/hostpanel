namespace Innovayse.Application.Clients.Commands.RemoveContact;

/// <summary>
/// Command to remove a contact from a client account.
/// </summary>
/// <param name="ClientId">The owning client's primary key.</param>
/// <param name="ContactId">The contact's primary key.</param>
public record RemoveContactCommand(int ClientId, int ContactId);
