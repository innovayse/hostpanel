namespace Innovayse.API.Provisioning;

/// <summary>Request body for changing a hosting account password.</summary>
public sealed class ChangePasswordRequest
{
    /// <summary>Gets the new password to set on the hosting account.</summary>
    public required string NewPassword { get; init; }
}
