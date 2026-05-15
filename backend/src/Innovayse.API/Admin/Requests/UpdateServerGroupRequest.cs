namespace Innovayse.API.Admin.Requests;
using Innovayse.Domain.Servers;

/// <summary>
/// Request body for updating an existing server group.
/// </summary>
public sealed class UpdateServerGroupRequest
{
    /// <summary>Gets new group display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets new fill strategy.</summary>
    public required ServerFillType FillType { get; init; }

    /// <summary>Gets the complete new list of server IDs for this group.</summary>
    public List<int> ServerIds { get; init; } = [];
}
