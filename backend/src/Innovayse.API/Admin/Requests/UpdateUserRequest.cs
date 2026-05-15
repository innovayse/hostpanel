namespace Innovayse.API.Admin.Requests;

/// <summary>Request body for updating a user's profile.</summary>
/// <param name="FirstName">New first name.</param>
/// <param name="LastName">New last name.</param>
/// <param name="Email">New email address.</param>
/// <param name="Language">Preferred language code (en, ru, hy) or null for default.</param>
public record UpdateUserRequest(string FirstName, string LastName, string Email, string? Language);
