namespace Innovayse.Domain.Clients;

using Innovayse.Domain.Common;

/// <summary>
/// Represents a non-owner user linked to a client account with granular permissions.
/// The account owner is identified by <see cref="Client.UserId"/> and always has all permissions.
/// Additional users are stored in the <c>client_users</c> table.
/// </summary>
public sealed class ClientUser : Entity
{
    /// <summary>Gets the FK to the client account.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the ASP.NET Core Identity user ID.</summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>Gets the granted permissions as a bit-flags value.</summary>
    public ClientPermission Permissions { get; private set; }

    /// <summary>Gets the UTC timestamp when this link was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private ClientUser() : base(0) { }

    /// <summary>
    /// Creates a new client-user link with the specified permissions.
    /// </summary>
    /// <param name="clientId">FK to the client account.</param>
    /// <param name="userId">Identity user ID.</param>
    /// <param name="permissions">Granted permissions.</param>
    /// <returns>A new <see cref="ClientUser"/> instance.</returns>
    internal static ClientUser Create(int clientId, string userId, ClientPermission permissions)
    {
        return new ClientUser
        {
            ClientId = clientId,
            UserId = userId,
            Permissions = permissions,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Updates the granted permissions.
    /// </summary>
    /// <param name="permissions">New permissions value.</param>
    internal void UpdatePermissions(ClientPermission permissions)
    {
        Permissions = permissions;
    }
}
