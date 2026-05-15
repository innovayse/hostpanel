namespace Innovayse.API.Auth.Requests;

/// <summary>HTTP request body for POST /api/auth/register.</summary>
/// <param name="Email">The user's email address.</param>
/// <param name="Password">The user's password (min 8 characters).</param>
/// <param name="FirstName">The user's first name.</param>
/// <param name="LastName">The user's last name.</param>
public record RegisterRequest(string Email, string Password, string FirstName, string LastName);
