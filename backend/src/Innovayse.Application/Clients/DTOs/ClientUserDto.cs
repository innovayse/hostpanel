namespace Innovayse.Application.Clients.DTOs;

/// <summary>DTO representing a user linked to a client with their permissions.</summary>
/// <param name="UserId">Identity user ID.</param>
/// <param name="FirstName">User's first name.</param>
/// <param name="LastName">User's last name.</param>
/// <param name="Email">User's email address.</param>
/// <param name="IsOwner">True if this user is the account owner.</param>
/// <param name="Permissions">Granted permissions as a bit-flags integer. 8191 (All) for owners.</param>
/// <param name="LastLoginAt">Last login timestamp or null.</param>
/// <param name="CreatedAt">When the user was linked to the client.</param>
public record ClientUserDto(
    string UserId,
    string FirstName,
    string LastName,
    string Email,
    bool IsOwner,
    int Permissions,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt);
