namespace Innovayse.API.Admin.Requests;
using Innovayse.Domain.Servers;

/// <summary>
/// Request body for creating or updating a provisioning server.
/// </summary>
public class CreateServerRequest
{
    /// <summary>Gets display name of the server.</summary>
    public required string Name { get; init; }

    /// <summary>Gets hostname or IP address for API access.</summary>
    public required string Hostname { get; init; }

    /// <summary>Gets primary public IP address, or null.</summary>
    public string? IpAddress { get; init; }

    /// <summary>Gets additional assigned IP addresses (one per line).</summary>
    public string? AssignedIpAddresses { get; init; }

    /// <summary>Gets the control panel module type.</summary>
    public required ServerModule Module { get; init; }

    /// <summary>Gets the API username.</summary>
    public required string Username { get; init; }

    /// <summary>Gets the API password (null = leave unchanged on update).</summary>
    public string? Password { get; init; }

    /// <summary>Gets the API token (null = leave unchanged on update).</summary>
    public string? ApiToken { get; init; }

    /// <summary>Gets the access hash for cPanel/CWP authentication (null = leave unchanged on update).</summary>
    public string? AccessHash { get; init; }

    /// <summary>Gets whether SSL should be used when connecting to the server API.</summary>
    public bool UseSSL { get; init; }

    /// <summary>Gets the maximum account limit, or null for unlimited.</summary>
    public int? MaxAccounts { get; init; }

    /// <summary>Gets whether this is the default server for the module.</summary>
    public bool IsDefault { get; init; }

    /// <summary>Gets whether this server is disabled.</summary>
    public bool IsDisabled { get; init; }

    /// <summary>Gets the monthly cost in USD.</summary>
    public decimal MonthlyCost { get; init; }

    /// <summary>Gets the datacenter or NOC provider name.</summary>
    public string? Datacenter { get; init; }

    /// <summary>Gets the URL to the server status page folder.</summary>
    public string? ServerStatusAddress { get; init; }

    /// <summary>Gets primary nameserver hostname.</summary>
    public string? Ns1Hostname { get; init; }

    /// <summary>Gets primary nameserver IP.</summary>
    public string? Ns1Ip { get; init; }

    /// <summary>Gets secondary nameserver hostname.</summary>
    public string? Ns2Hostname { get; init; }

    /// <summary>Gets secondary nameserver IP.</summary>
    public string? Ns2Ip { get; init; }

    /// <summary>Gets third nameserver hostname.</summary>
    public string? Ns3Hostname { get; init; }

    /// <summary>Gets third nameserver IP.</summary>
    public string? Ns3Ip { get; init; }

    /// <summary>Gets fourth nameserver hostname.</summary>
    public string? Ns4Hostname { get; init; }

    /// <summary>Gets fourth nameserver IP.</summary>
    public string? Ns4Ip { get; init; }

    /// <summary>Gets fifth nameserver hostname.</summary>
    public string? Ns5Hostname { get; init; }

    /// <summary>Gets fifth nameserver IP.</summary>
    public string? Ns5Ip { get; init; }

    /// <summary>
    /// Converts this request to a domain <see cref="ServerDetails"/> value object.
    /// </summary>
    /// <returns>The mapped <see cref="ServerDetails"/>.</returns>
    public ServerDetails ToDetails() => new(
        Name, Hostname, IpAddress, AssignedIpAddresses,
        Module, Username, Password, ApiToken, AccessHash,
        UseSSL, MaxAccounts, IsDefault, IsDisabled,
        MonthlyCost, Datacenter, ServerStatusAddress,
        Ns1Hostname, Ns1Ip, Ns2Hostname, Ns2Ip,
        Ns3Hostname, Ns3Ip, Ns4Hostname, Ns4Ip,
        Ns5Hostname, Ns5Ip);
}
