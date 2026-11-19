using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Services;

/// <summary>
/// What a plugin set up for a client on a hosting service's server: a website or a database. The base holds what both
/// share, which is the service, the pinned server and account, the plugin it was issued to and the state.
/// </summary>
/// <remarks>
/// A resource is <b>pinned</b> to the server and Linux account it was created for
/// (<see cref="ServerId"/>, <see cref="AccountUsername"/>), copied from the service at creation and never updated.
/// If an administrator later moves the service or renames the account, <see cref="IsBoundTo"/> answers false and no
/// operation is sent anywhere for this resource, because the same account name on another server can belong to
/// someone else entirely. No resource keeps a password: the one a provider returns is shown once and not stored.
/// </remarks>
public abstract class HostingResource : BaseEntity, IAggregateRoot
{
    /// <summary>Gets the client service this resource was created for.</summary>
    public int ClientServiceId { get; private set; }

    /// <summary>Gets the server the resource was created on. Pinned at creation.</summary>
    public int ServerId { get; private set; }

    /// <summary>Gets the Linux account on <see cref="ServerId"/> the resource belongs to. Pinned at creation.</summary>
    public string AccountUsername { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the id of the plugin the host issued this resource to. Set once: only that plugin may remove the resource
    /// through the plugin host API.
    /// </summary>
    public string IssuedToPluginId { get; private set; } = string.Empty;

    /// <summary>Gets the synchronisation state with the server.</summary>
    public HostingResourceState State { get; private set; }

    /// <summary>EF Core constructor.</summary>
    protected HostingResource()
    {
    }

    /// <summary>Records a new resource in <see cref="HostingResourceState.Pending"/>, before the provider is called.</summary>
    /// <param name="clientServiceId">The owning service.</param>
    /// <param name="serverId">The server the service is assigned to right now.</param>
    /// <param name="accountUsername">The Linux account on that server right now.</param>
    /// <param name="issuedToPluginId">The plugin the host issues the resource to.</param>
    /// <exception cref="ArgumentException">A text is blank.</exception>
    protected HostingResource(int clientServiceId, int serverId, string accountUsername, string issuedToPluginId)
    {
        EnsureText(accountUsername, nameof(accountUsername));
        // Only blank is refused here: the plugin id rule (shape, length, reserved ids) belongs to the plugin packaging package,
        // which the domain does not reference, and the id reaching this point is a loaded plugin's, already checked at load.
        EnsureText(issuedToPluginId, nameof(issuedToPluginId));

        ClientServiceId = clientServiceId;
        ServerId = serverId;
        AccountUsername = accountUsername;
        IssuedToPluginId = issuedToPluginId;
        State = HostingResourceState.Pending;
    }

    /// <summary>Whether the host issued this resource to <paramref name="pluginId"/>.</summary>
    /// <param name="pluginId">The plugin asking.</param>
    /// <returns><see langword="true"/> when the resource was issued to exactly that plugin (ordinal comparison).</returns>
    public bool IsIssuedTo(string pluginId)
    {
        return string.Equals(IssuedToPluginId, pluginId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Whether this resource was created for exactly this server and account. Every provider call for the resource
    /// must be preceded by this check.
    /// </summary>
    /// <param name="serverId">The service's current server.</param>
    /// <param name="accountUsername">The service's current account.</param>
    /// <returns><see langword="true"/> when both match the pinned values.</returns>
    public bool IsBoundTo(int? serverId, string? accountUsername)
    {
        return serverId == ServerId && string.Equals(accountUsername, AccountUsername, StringComparison.Ordinal);
    }

    /// <summary>Records that the provider confirmed the resource exists.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the resource is not pending.</exception>
    public void MarkActive()
    {
        if (State is not HostingResourceState.Pending)
        {
            throw new InvalidOperationException("Only a pending hosting resource can become active.");
        }

        State = HostingResourceState.Active;
    }

    /// <summary>Records that the removal failed; the resource may still exist on the server.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the resource is already removed.</exception>
    public void MarkRemoveFailed()
    {
        EnsureNotRemoved();
        State = HostingResourceState.RemoveFailed;
    }

    /// <summary>Records that the provider confirmed the resource is gone from the server.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the resource is already removed.</exception>
    public void MarkRemoved()
    {
        EnsureNotRemoved();
        State = HostingResourceState.Removed;
    }

    /// <summary>Refuses a blank required text, the last line of defence behind the use cases' own checks.</summary>
    /// <param name="value">The value.</param>
    /// <param name="name">The parameter's name, for the exception.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty or white space.</exception>
    protected static void EnsureText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", name);
        }
    }

    /// <summary>Refuses any transition out of the terminal state.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the resource is already removed.</exception>
    private void EnsureNotRemoved()
    {
        if (State is HostingResourceState.Removed)
        {
            throw new InvalidOperationException("Hosting resource is already removed.");
        }
    }
}
