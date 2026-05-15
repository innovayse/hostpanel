namespace Innovayse.API.Admin.Requests;

/// <summary>Request body for updating a configuration setting value.</summary>
public sealed class UpdateSettingRequest
{
    /// <summary>Gets or initializes the new value to assign to the setting.</summary>
    public required string Value { get; init; }
}
