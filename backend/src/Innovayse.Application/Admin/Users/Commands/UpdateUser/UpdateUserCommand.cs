namespace Innovayse.Application.Admin.Users.Commands.UpdateUser;

/// <summary>Command to update a user's profile and sign-in address (admin action).</summary>
/// <param name="Id">The person's subject, taken from the route rather than the body.</param>
/// <param name="FirstName">New first name.</param>
/// <param name="LastName">New last name.</param>
/// <param name="Email">New email address.</param>
/// <param name="Language">Preferred language code (en, ru, hy) or null for default.</param>
public record UpdateUserCommand(
    string Id,
    string FirstName,
    string LastName,
    string Email,
    string? Language);
