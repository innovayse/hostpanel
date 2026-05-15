namespace Innovayse.API.Support.Requests;

/// <summary>Request body for creating a support department.</summary>
public sealed class CreateDepartmentRequest
{
    /// <summary>Gets or sets the department display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets or sets the department email address.</summary>
    public required string Email { get; init; }
}
