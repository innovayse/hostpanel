namespace Innovayse.Application.Clients.DTOs;

using Innovayse.Domain.Clients;

/// <summary>Summary DTO for client list rows.</summary>
/// <param name="Id">Client primary key.</param>
/// <param name="UserId">Linked Identity user ID.</param>
/// <param name="Email">Email address from the linked Identity user.</param>
/// <param name="FirstName">Client first name.</param>
/// <param name="LastName">Client last name.</param>
/// <param name="CompanyName">Company name (null for individuals).</param>
/// <param name="Status">Current account status.</param>
/// <param name="IsUserDeleted">True if the linked Identity user no longer exists.</param>
/// <param name="CreatedAt">Account creation timestamp.</param>
public record ClientListItemDto(
    int Id,
    string UserId,
    string Email,
    string FirstName,
    string LastName,
    string? CompanyName,
    ClientStatus Status,
    bool IsUserDeleted,
    DateTimeOffset CreatedAt);
