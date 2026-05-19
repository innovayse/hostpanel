namespace Innovayse.API.Provisioning;

/// <summary>Request body for changing a hosting account package.</summary>
public sealed class ChangePackageRequest
{
    /// <summary>Gets the name of the new hosting package to assign.</summary>
    public required string NewPackage { get; init; }
}
