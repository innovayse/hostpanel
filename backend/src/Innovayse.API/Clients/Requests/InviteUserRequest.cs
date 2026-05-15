namespace Innovayse.API.Clients.Requests;

/// <summary>HTTP request body for inviting a user to a client account.</summary>
/// <param name="Email">The invitee's email address.</param>
/// <param name="FirstName">The invitee's first name.</param>
/// <param name="LastName">The invitee's last name.</param>
/// <param name="Permissions">Permissions to grant as bit-flags integer.</param>
public record InviteUserRequest(string Email, string FirstName, string LastName, int Permissions);
