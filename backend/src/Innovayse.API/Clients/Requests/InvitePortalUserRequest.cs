namespace Innovayse.API.Clients.Requests;

/// <summary>
/// HTTP request body for a client inviting someone to their own account.
/// </summary>
/// <param name="Email">The invitee's email address.</param>
/// <param name="FirstName">The invitee's first name.</param>
/// <param name="LastName">The invitee's last name.</param>
/// <param name="Permissions">
/// What the invitee may do: the literal <c>all</c>, or a comma-separated list of permission
/// keys. See <see cref="MyUsersController"/> for the keys and why this is not the bit-flags
/// integer the admin endpoint takes.
/// </param>
public record InvitePortalUserRequest(
    string Email, string FirstName, string LastName, string? Permissions);
