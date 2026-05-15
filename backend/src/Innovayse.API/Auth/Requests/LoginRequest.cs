namespace Innovayse.API.Auth.Requests;

/// <summary>HTTP request body for POST /api/auth/login.</summary>
/// <param name="Email">The user's email address.</param>
/// <param name="Password">The user's password.</param>
public record LoginRequest(string Email, string Password);
