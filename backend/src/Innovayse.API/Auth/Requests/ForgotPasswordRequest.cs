namespace Innovayse.API.Auth.Requests;

/// <summary>HTTP request body for POST /api/auth/forgot-password.</summary>
/// <param name="Email">The user's email address.</param>
public record ForgotPasswordRequest(string Email);
