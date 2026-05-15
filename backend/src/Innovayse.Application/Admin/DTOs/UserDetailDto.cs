namespace Innovayse.Application.Admin.DTOs;

/// <summary>Full user DTO including linked client accounts.</summary>
/// <param name="Id">Identity user ID.</param>
/// <param name="FirstName">User first name.</param>
/// <param name="LastName">User last name.</param>
/// <param name="Email">User email address.</param>
/// <param name="Language">Preferred UI language code, or null for default.</param>
/// <param name="LastLoginAt">UTC timestamp of last login, or null if never.</param>
/// <param name="CreatedAt">UTC timestamp of account creation.</param>
/// <param name="Accounts">Client accounts linked to this user.</param>
public record UserDetailDto(
    string Id,
    string FirstName,
    string LastName,
    string Email,
    string? Language,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<UserAccountDto> Accounts);
