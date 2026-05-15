namespace Innovayse.Domain.Servers;
using Innovayse.Domain.Common;

/// <summary>
/// Represents a named group of provisioning servers with a shared fill strategy.
/// </summary>
public sealed class ServerGroup : AggregateRoot
{
    /// <summary>Gets the display name of this group.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the strategy used to assign new accounts across servers in this group.</summary>
    public ServerFillType FillType { get; private set; }

    /// <summary>Gets the UTC timestamp when the group was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Navigation property — servers that belong to this group.</summary>
    public IReadOnlyCollection<Server> Servers => _servers.AsReadOnly();

    /// <summary>Mutable backing list for EF Core.</summary>
    private readonly List<Server> _servers = [];

    /// <summary>EF Core constructor — do not call directly.</summary>
    private ServerGroup() : base(0) { }

    /// <summary>Initialises a ServerGroup with the given identity.</summary>
    /// <param name="id">Entity identifier.</param>
    private ServerGroup(int id) : base(id) { }

    /// <summary>
    /// Creates a new server group.
    /// </summary>
    /// <param name="name">Display name for the group.</param>
    /// <param name="fillType">Account distribution strategy.</param>
    /// <returns>A new <see cref="ServerGroup"/> instance.</returns>
    public static ServerGroup Create(string name, ServerFillType fillType)
    {
        return new ServerGroup(0)
        {
            Name = name,
            FillType = fillType,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Updates the group name and fill strategy.
    /// </summary>
    /// <param name="name">New display name.</param>
    /// <param name="fillType">New fill strategy.</param>
    public void Update(string name, ServerFillType fillType)
    {
        Name = name;
        FillType = fillType;
    }
}
