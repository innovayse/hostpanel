namespace Innovayse.Application.Admin.DTOs;

/// <summary>Client account linked to an Identity user.</summary>
/// <param name="ClientId">Client primary key.</param>
/// <param name="FirstName">Client first name.</param>
/// <param name="LastName">Client last name.</param>
/// <param name="CompanyName">Company name, or null for individuals.</param>
/// <param name="IsOwner">True if this user owns the client account.</param>
public record UserAccountDto(
    int ClientId,
    string FirstName,
    string LastName,
    string? CompanyName,
    bool IsOwner);
