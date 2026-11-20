namespace Innovayse.Hostpanel.Domain.Services;

/// <summary>
/// One database a plugin set up for a client on a hosting service's server. The database user's password is never
/// kept: the provider returns it once, when the database is created.
/// </summary>
public sealed class ServiceDatabase : HostingResource
{
    /// <summary>
    /// Gets the stable public identifier of this database. The provider is addressed by it and the API routes use it:
    /// the sequential database id is never exposed.
    /// </summary>
    public Guid DatabaseId { get; private set; }

    /// <summary>Gets the label the plugin gave the database, unique among the service's live databases of that plugin.</summary>
    public string Label { get; private set; } = string.Empty;

    /// <summary>Gets the database name on the server. Empty until the provider has named it.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the database user's name on the server. Empty until the provider has named it.</summary>
    public string Username { get; private set; } = string.Empty;

    /// <summary>EF Core constructor.</summary>
    private ServiceDatabase()
    {
    }

    /// <summary>Builds the pending database once <see cref="Create"/> has checked the label.</summary>
    /// <param name="clientServiceId">The owning service.</param>
    /// <param name="serverId">The pinned server.</param>
    /// <param name="accountUsername">The pinned account.</param>
    /// <param name="issuedToPluginId">The plugin it is issued to.</param>
    /// <param name="label">The label.</param>
    private ServiceDatabase(int clientServiceId, int serverId, string accountUsername, string issuedToPluginId, string label)
        : base(clientServiceId, serverId, accountUsername, issuedToPluginId)
    {
        DatabaseId = Guid.NewGuid();
        Label = label.Trim();
    }

    /// <summary>Records the database being created, before the provider has named it.</summary>
    /// <param name="clientServiceId">The owning service.</param>
    /// <param name="serverId">The server the service is assigned to right now.</param>
    /// <param name="accountUsername">The Linux account on that server right now.</param>
    /// <param name="issuedToPluginId">The plugin the host issues the database to.</param>
    /// <param name="label">The label; trimmed.</param>
    /// <returns>The new, pending database.</returns>
    /// <exception cref="ArgumentException">A required text is blank.</exception>
    public static ServiceDatabase Create(
        int clientServiceId, int serverId, string accountUsername, string issuedToPluginId, string label)
    {
        EnsureText(label, nameof(label));

        return new ServiceDatabase(clientServiceId, serverId, accountUsername, issuedToPluginId, label);
    }

    /// <summary>Records the names the provider gave the database and confirms it exists.</summary>
    /// <param name="name">The database name on the server.</param>
    /// <param name="username">The database user's name on the server.</param>
    /// <exception cref="ArgumentException">A name is blank.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the database is not pending.</exception>
    public void MarkProvisioned(string name, string username)
    {
        EnsureText(name, nameof(name));
        EnsureText(username, nameof(username));

        MarkActive();
        Name = name.Trim();
        Username = username.Trim();
    }
}
