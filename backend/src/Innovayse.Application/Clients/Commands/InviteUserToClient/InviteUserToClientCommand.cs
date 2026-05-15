namespace Innovayse.Application.Clients.Commands.InviteUserToClient;

/// <summary>Command to invite a user to a client account via email.</summary>
/// <param name="ClientId">The client's primary key.</param>
/// <param name="Email">The invitee's email address.</param>
/// <param name="FirstName">The invitee's first name.</param>
/// <param name="LastName">The invitee's last name.</param>
/// <param name="Permissions">Permissions to grant as bit-flags integer.</param>
public record InviteUserToClientCommand(int ClientId, string Email, string FirstName, string LastName, int Permissions);
