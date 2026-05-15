namespace Innovayse.Application.Admin.DTOs;

/// <summary>Summary DTO for admin user list rows.</summary>
/// <param name="Id">Identity user ID.</param>
/// <param name="ClientId">Linked client primary key, or null if no client record.</param>
/// <param name="FirstName">User first name.</param>
/// <param name="LastName">User last name.</param>
/// <param name="Email">User email address.</param>
/// <param name="Language">Preferred UI language code, or null for default.</param>
/// <param name="LastLoginAt">UTC timestamp of last login, or null if never.</param>
/// <param name="CreatedAt">UTC timestamp of account creation.</param>
public record UserListItemDto(
    string Id,
    int? ClientId,
    string FirstName,
    string LastName,
    string Email,
    string? Language,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt);
