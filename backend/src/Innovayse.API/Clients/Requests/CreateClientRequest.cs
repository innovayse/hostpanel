namespace Innovayse.API.Clients.Requests;

/// <summary>HTTP request body for POST /api/clients.</summary>
/// <param name="UserId">The Identity user ID to link to this client.</param>
/// <param name="Email">The user's email address.</param>
/// <param name="FirstName">Client's first name.</param>
/// <param name="LastName">Client's last name.</param>
/// <param name="CompanyName">Optional company name.</param>
public record CreateClientRequest(
    string UserId,
    string Email,
    string FirstName,
    string LastName,
    string? CompanyName);
