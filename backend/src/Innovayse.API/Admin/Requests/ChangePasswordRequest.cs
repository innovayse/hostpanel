namespace Innovayse.API.Admin.Requests;

/// <summary>Request body for admin-initiated password change.</summary>
/// <param name="Password">The new password to set.</param>
public record ChangePasswordRequest(string Password);
