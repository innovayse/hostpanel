namespace Innovayse.API.Auth.Requests;

/// <summary>HTTP request body for POST /api/auth/reset-password.</summary>
/// <param name="Email">The user's email address.</param>
/// <param name="Token">The password reset token from the email link.</param>
/// <param name="NewPassword">The new password to set.</param>
public record ResetPasswordRequest(string Email, string Token, string NewPassword);
