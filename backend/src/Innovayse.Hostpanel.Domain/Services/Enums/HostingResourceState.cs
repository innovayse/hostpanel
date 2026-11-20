namespace Innovayse.Hostpanel.Domain.Services.Enums;

/// <summary>
/// Where a <see cref="HostingResource"/> stands relative to the hosting server it was issued for.
/// </summary>
/// <remarks>
/// The server is the source of truth for what exists. The state records what HostPanel last <i>observed</i>
/// there, so a provider failure is never mistaken for success: a resource is only <see cref="Active"/> once the
/// provider confirmed it, and only <see cref="Removed"/> once the provider confirmed its removal.
/// </remarks>
public enum HostingResourceState
{
    /// <summary>Recorded; the provider has not yet confirmed it created the resource.</summary>
    Pending,

    /// <summary>The provider confirmed the resource exists on the server.</summary>
    Active,

    /// <summary>The removal call failed or timed out; the resource may still exist on the server.</summary>
    RemoveFailed,

    /// <summary>The provider confirmed the resource is gone from the server. Terminal.</summary>
    Removed,
}
