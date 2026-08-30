namespace Innovayse.API.Auth.Requests;

/// <summary>HTTP request body for POST /api/auth/confirm-email.</summary>
/// <param name="Email">The user's email address.</param>
/// <param name="Token">The email confirmation token sent during registration.</param>
public record ConfirmEmailRequest(string Email, string Token);
