namespace Innovayse.Application.Auth.Commands.Register;

/// <summary>
/// Command to register a new client account.
/// On success returns an <see cref="Innovayse.Application.Auth.DTOs.AuthWithRefreshDto"/> with access and refresh tokens.
/// </summary>
/// <param name="Email">The user's email address. Must be unique.</param>
/// <param name="Password">Plain-text password. Will be hashed by Identity.</param>
/// <param name="FirstName">User's first name.</param>
/// <param name="LastName">User's last name.</param>
public record RegisterCommand(string Email, string Password, string FirstName, string LastName);
