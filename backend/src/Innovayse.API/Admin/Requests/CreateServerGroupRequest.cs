namespace Innovayse.API.Admin.Requests;
using Innovayse.Domain.Servers;

/// <summary>
/// Request body for creating a new server group.
/// </summary>
public sealed class CreateServerGroupRequest
{
    /// <summary>Gets the group display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the fill strategy.</summary>
    public required ServerFillType FillType { get; init; }

    /// <summary>Gets the IDs of servers to assign to this group.</summary>
    public List<int> ServerIds { get; init; } = [];
}
