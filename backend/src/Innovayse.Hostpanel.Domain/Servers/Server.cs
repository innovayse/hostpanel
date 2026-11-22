using Innovayse.Hostpanel.Domain.Servers.Events;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Servers;

/// <summary>
/// Represents a provisioning server that hosts client accounts.
/// </summary>
public sealed class Server : BaseEntity, IAggregateRoot
{
    /// <summary>Gets the display name of this server.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the hostname or IP address used to connect to the server API.</summary>
    public string Hostname { get; private set; } = string.Empty;

    /// <summary>Gets the primary IP address of the server, or null if not set.</summary>
    public string? IpAddress { get; private set; }

    /// <summary>Gets additional assigned IP addresses (one per line), or null.</summary>
    public string? AssignedIpAddresses { get; private set; }

    /// <summary>Gets the control panel module id managing this server: a built in id (see <see cref="ServerModules"/>) or one a panel plugin declares.</summary>
    public string Module { get; private set; } = string.Empty;

    /// <summary>Gets the API username for authenticating with the server.</summary>
    public string Username { get; private set; } = string.Empty;

    /// <summary>Gets the encrypted API password or secret key.</summary>
    public string? Password { get; private set; }

    /// <summary>Gets the API token used in place of username/password when supported.</summary>
    public string? ApiToken { get; private set; }

    /// <summary>Gets the access hash (used by hosting panels for token-based auth).</summary>
    public string? AccessHash { get; private set; }

    /// <summary>Gets whether SSL should be used when connecting to the server API.</summary>
    public bool UseSSL { get; private set; }

    /// <summary>
    /// Gets whether the operator trusts the self signed certificate of this server's API. The plugin transport then accepts
    /// that certificate for this server's own host name and nothing else; it is off by default.
    /// </summary>
    public bool AcceptsSelfSignedCertificate { get; private set; }

    /// <summary>Gets the maximum number of accounts allowed on this server. Null means unlimited.</summary>
    public int? MaxAccounts { get; private set; }

    /// <summary>Gets whether this is the default server for its module type.</summary>
    public bool IsDefault { get; private set; }

    /// <summary>Gets whether this server is disabled and should not receive new accounts.</summary>
    public bool IsDisabled { get; private set; }

    /// <summary>Gets the monthly cost of this server in USD.</summary>
    public decimal MonthlyCost { get; private set; }

    /// <summary>Gets the datacenter or NOC provider name.</summary>
    public string? Datacenter { get; private set; }

    /// <summary>Gets the URL to this server's status page folder.</summary>
    public string? ServerStatusAddress { get; private set; }

    // --- Nameservers ---

    /// <summary>Gets the primary nameserver hostname.</summary>
    public string? Ns1Hostname { get; private set; }

    /// <summary>Gets the primary nameserver IP address.</summary>
    public string? Ns1Ip { get; private set; }

    /// <summary>Gets the secondary nameserver hostname.</summary>
    public string? Ns2Hostname { get; private set; }

    /// <summary>Gets the secondary nameserver IP address.</summary>
    public string? Ns2Ip { get; private set; }

    /// <summary>Gets the third nameserver hostname.</summary>
    public string? Ns3Hostname { get; private set; }

    /// <summary>Gets the third nameserver IP address.</summary>
    public string? Ns3Ip { get; private set; }

    /// <summary>Gets the fourth nameserver hostname.</summary>
    public string? Ns4Hostname { get; private set; }

    /// <summary>Gets the fourth nameserver IP address.</summary>
    public string? Ns4Ip { get; private set; }

    /// <summary>Gets the fifth nameserver hostname.</summary>
    public string? Ns5Hostname { get; private set; }

    /// <summary>Gets the fifth nameserver IP address.</summary>
    public string? Ns5Ip { get; private set; }

    /// <summary>Gets the ID of the server group this server belongs to, or null if ungrouped.</summary>
    public int? ServerGroupId { get; private set; }

    /// <summary>Gets the UTC timestamp when the server was added.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets whether the last connection test succeeded, or null if never tested.</summary>
    public bool? IsOnline { get; private set; }

    /// <summary>Gets the UTC timestamp of the last connection test, or null if never tested.</summary>
    public DateTimeOffset? LastTestedAt { get; private set; }

    /// <summary>Gets the account count reported by the server on last test, or null if unknown.</summary>
    public int? AccountsCount { get; private set; }

    /// <summary>Gets whether an API password is stored.</summary>
    public bool HasPassword => !string.IsNullOrEmpty(Password);

    /// <summary>Gets whether an API token is stored.</summary>
    public bool HasApiToken => !string.IsNullOrEmpty(ApiToken);

    /// <summary>Gets whether an access hash is stored.</summary>
    public bool HasAccessHash => !string.IsNullOrEmpty(AccessHash);

    /// <summary>EF Core constructor: do not call directly.</summary>
    private Server() { }

    /// <summary>Initialises a Server with the given identity.</summary>
    /// <param name="id">Entity identifier.</param>
    private Server(int id)
    {
        Id = id;
    }

    /// <summary>
    /// Creates a new server and raises <see cref="ServerCreatedEvent"/>.
    /// </summary>
    /// <param name="details">All server configuration fields.</param>
    /// <param name="occurredAt">The moment the server was added, UTC.</param>
    /// <returns>A new <see cref="Server"/> instance.</returns>
    public static Server Create(ServerDetails details, DateTimeOffset occurredAt)
    {
        var server = new Server(0);
        server.Apply(details);
        server.AddDomainEvent(new ServerCreatedEvent(details.Name, details.Module) { OccurredAt = occurredAt });
        return server;
    }

    /// <summary>
    /// Reconfigures the server as one edit of its connection settings. A credential left null, empty
    /// or whitespace in <paramref name="details"/> keeps its stored value; one named in
    /// <paramref name="clears"/> is removed, and a clear wins over a value sent alongside it.
    /// </summary>
    /// <param name="details">The new server configuration.</param>
    /// <param name="clears">The stored credentials to remove.</param>
    public void Reconfigure(ServerDetails details, ServerCredentialClears clears)
    {
        ArgumentNullException.ThrowIfNull(clears);

        Apply(details);
        if (clears.Password)
        {
            Password = null;
        }

        if (clears.ApiToken)
        {
            ApiToken = null;
        }

        if (clears.AccessHash)
        {
            AccessHash = null;
        }
    }

    /// <summary>
    /// Assigns this server to a group.
    /// </summary>
    /// <param name="groupId">The group identifier.</param>
    public void AssignToGroup(int groupId) => ServerGroupId = groupId;

    /// <summary>
    /// Removes this server from its current group.
    /// </summary>
    public void RemoveFromGroup() => ServerGroupId = null;

    /// <summary>
    /// Records the result of a connection test.
    /// </summary>
    /// <param name="isOnline">Whether the connection succeeded.</param>
    /// <param name="accountsCount">Account count from the server, or null on failure.</param>
    /// <param name="testedAt">When the test ran, UTC.</param>
    public void RecordConnectionTest(bool isOnline, int? accountsCount, DateTimeOffset testedAt)
    {
        IsOnline = isOnline;
        LastTestedAt = testedAt;
        AccountsCount = accountsCount;
    }

    /// <summary>
    /// Applies all fields from a <see cref="ServerDetails"/> value object.
    /// </summary>
    /// <param name="d">The details to apply.</param>
    /// <exception cref="DomainRuleException">The details accept a self signed certificate on a server without SSL.</exception>
    private void Apply(ServerDetails d)
    {
        if (d.AcceptsSelfSignedCertificate && !d.UseSSL)
        {
            throw new DomainRuleException(
                ServerErrors.SelfSignedRequiresSsl,
                "A server without SSL cannot accept a self signed certificate.");
        }

        Name = d.Name;
        Hostname = d.Hostname;
        IpAddress = d.IpAddress;
        AssignedIpAddresses = d.AssignedIpAddresses;
        Module = d.Module;
        Username = d.Username;
        // A blank credential is "unchanged", never "erase": the admin form never holds the stored
        // value, so a field left empty on save must not wipe it. Removal is an explicit clear.
        if (!string.IsNullOrWhiteSpace(d.Credentials.Password))
        {
            Password = d.Credentials.Password;
        }

        if (!string.IsNullOrWhiteSpace(d.Credentials.ApiToken))
        {
            ApiToken = d.Credentials.ApiToken;
        }

        if (!string.IsNullOrWhiteSpace(d.Credentials.AccessHash))
        {
            AccessHash = d.Credentials.AccessHash;
        }

        UseSSL = d.UseSSL;
        AcceptsSelfSignedCertificate = d.AcceptsSelfSignedCertificate;
        MaxAccounts = d.MaxAccounts;
        IsDefault = d.IsDefault;
        IsDisabled = d.IsDisabled;
        MonthlyCost = d.MonthlyCost;
        Datacenter = d.Datacenter;
        ServerStatusAddress = d.ServerStatusAddress;
        Ns1Hostname = d.NameServers.Ns1Hostname;
        Ns1Ip = d.NameServers.Ns1Ip;
        Ns2Hostname = d.NameServers.Ns2Hostname;
        Ns2Ip = d.NameServers.Ns2Ip;
        Ns3Hostname = d.NameServers.Ns3Hostname;
        Ns3Ip = d.NameServers.Ns3Ip;
        Ns4Hostname = d.NameServers.Ns4Hostname;
        Ns4Ip = d.NameServers.Ns4Ip;
        Ns5Hostname = d.NameServers.Ns5Hostname;
        Ns5Ip = d.NameServers.Ns5Ip;
    }
}
